using InterviewPrepAPI.Data;
using InterviewPrepAPI.Localization;
using InterviewPrepAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Localization;

namespace InterviewPrepAPI.Services;

public interface IAuthService
{
    Task RegisterAsync(RegisterDto dto, HttpContext context);
    Task<AuthResult> VerifyEmailAsync(VerifyOtpDto dto, HttpContext context);
    Task<AuthResult> LoginAsync(LoginDto dto, HttpContext context);
    Task<AuthResult> RefreshTokensAsync(HttpContext context);
    Task RevokeRefreshTokenAsync(HttpContext context);
    Task<User?> GetUserByEmailAsync(string email);
    Task ResetPasswordAsync(string email, string newPassword);
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IOtpService _otpService;
    private readonly IEmailService _emailService;
    private readonly JwtSettings _jwtSettings;
    private readonly IStringLocalizer<Strings> _loc;

    public AuthService(AppDbContext db, ITokenService tokenService, IOtpService otpService, IEmailService emailService, IOptions<JwtSettings> jwtSettings, IStringLocalizer<Strings> loc)
    {
        _db = db;
        _tokenService = tokenService;
        _otpService = otpService;
        _emailService = emailService;
        _jwtSettings = jwtSettings.Value;
        _loc = loc;
    }

    public async Task RegisterAsync(RegisterDto dto, HttpContext context)
    {
        if (await _db.Users.AnyAsync(u => u.Email == dto.Email))
            throw new InvalidOperationException(_loc[Strings.Auth.UserAlreadyRegistered]);

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        var code = await _otpService.GenerateOtpAsync(dto.Email, ip, "email_verification", dto.Username, passwordHash);
        await _emailService.SendOtpEmailAsync(dto.Email, code);
    }

    public async Task<AuthResult> VerifyEmailAsync(VerifyOtpDto dto, HttpContext context)
    {
        var result = await _otpService.ValidateOtpAsync(dto.Email, dto.Code, "email_verification");
        if (!result.IsValid)
        {
            if (result.IsLocked)
                throw new InvalidOperationException(_loc[Strings.Password.VerifyLocked]);
            throw new InvalidOperationException(_loc[Strings.Password.VerifyInvalidOrExpired, result.RemainingAttempts]);
        }

        if (string.IsNullOrEmpty(result.Username) || string.IsNullOrEmpty(result.PasswordHash))
            throw new InvalidOperationException(_loc[Strings.Auth.UserNotFoundGeneric]);

        var user = new User
        {
            Username = result.Username,
            Email = dto.Email,
            PasswordHash = result.PasswordHash,
            AuthProvider = "local",
            EmailConfirmed = true
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return await GenerateTokensAsync(user, context);
    }

    public async Task<AuthResult> LoginAsync(LoginDto dto, HttpContext context)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.Email == dto.Email && u.AuthProvider == "local");

        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            throw new UnauthorizedAccessException(_loc[Strings.Auth.LoginInvalidCredentials]);

        if (!user.EmailConfirmed)
            throw new InvalidOperationException(_loc[Strings.Auth.EmailNotVerified]);

        return await GenerateTokensAsync(user, context);
    }

    public async Task<AuthResult> RefreshTokensAsync(HttpContext context)
    {
        var refreshToken = context.Request.Cookies["refresh_token"];
        if (string.IsNullOrEmpty(refreshToken))
            throw new UnauthorizedAccessException(_loc[Strings.Auth.TokenRefreshNotFound]);

        var storedToken = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t =>
                t.Token == refreshToken && !t.IsRevoked);

        if (storedToken == null || storedToken.ExpiresAt < DateTime.UtcNow)
            throw new UnauthorizedAccessException(_loc[Strings.Auth.TokenInvalidRefresh]);

        storedToken.IsRevoked = true;
        return await GenerateTokensAsync(storedToken.User, context);
    }

    public async Task RevokeRefreshTokenAsync(HttpContext context)
    {
        var refreshToken = context.Request.Cookies["refresh_token"];

        if (!string.IsNullOrEmpty(refreshToken))
        {
            var token = await _db.RefreshTokens
                .FirstOrDefaultAsync(t => t.Token == refreshToken);

            if (token != null)
            {
                token.IsRevoked = true;
                await _db.SaveChangesAsync();
            }
        }

        // Always clear cookies regardless of whether refresh token existed
        context.Response.Cookies.Delete("refresh_token", new CookieOptions
        {
            Path = "/api/auth",
            Secure = true,
            SameSite = SameSiteMode.Strict
        });
        context.Response.Cookies.Delete("access_token", new CookieOptions
        {
            Path = "/",
            Secure = true,
            SameSite = SameSiteMode.Strict
        });
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task ResetPasswordAsync(string email, string newPassword)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email)
            ?? throw new InvalidOperationException(_loc[Strings.Auth.UserNotFoundGeneric]);

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);

        var tokens = await _db.RefreshTokens
            .Where(t => t.UserId == user.Id && !t.IsRevoked)
            .ToListAsync();

        foreach (var token in tokens)
            token.IsRevoked = true;

        await _db.SaveChangesAsync();
    }

    private async Task<AuthResult> GenerateTokensAsync(User user, HttpContext context)
    {
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            Token = refreshToken.Token,
            ExpiresAt = refreshToken.ExpiresAt,
            UserId = user.Id
        });

        await _db.SaveChangesAsync();

        var accessCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes)
        };
        context.Response.Cookies.Append("access_token", accessToken, accessCookieOptions);

        var refreshCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth",
            Expires = refreshToken.ExpiresAt
        };
        context.Response.Cookies.Append("refresh_token", refreshToken.Token, refreshCookieOptions);

        return new AuthResult
        {
            AccessToken = accessToken,
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email
            }
        };
    }
}
