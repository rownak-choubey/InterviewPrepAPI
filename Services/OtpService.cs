using System.Security.Cryptography;
using InterviewPrepAPI.Data;
using InterviewPrepAPI.Localization;
using InterviewPrepAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace InterviewPrepAPI.Services;

public interface IOtpService
{
    Task<string> GenerateOtpAsync(User user, string ipAddress, string purpose = "password_reset");
    Task<string> GenerateOtpAsync(string email, string ipAddress, string purpose, string username, string passwordHash);
    Task<OtpValidationResult> ValidateOtpAsync(string email, string code, string purpose = "password_reset");
    Task<string> GenerateResetTokenAsync(string email, string ipAddress);
    Task<bool> ValidateResetTokenAsync(string email, string token);
}

public class OtpValidationResult
{
    public bool IsValid { get; set; }
    public bool IsLocked { get; set; }
    public int RemainingAttempts { get; set; }
    public string? ResetToken { get; set; }
    public string? Username { get; set; }
    public string? PasswordHash { get; set; }
}

public class OtpService : IOtpService
{
    private readonly AppDbContext _db;
    private readonly ILogger<OtpService> _logger;
    private readonly IStringLocalizer<Strings> _loc;

    private const int MaxAttempts = 5;
    private const int LockoutMinutes = 15;
    private const int ResetTokenExpiryMinutes = 5;

    public OtpService(AppDbContext db, ILogger<OtpService> logger, IStringLocalizer<Strings> loc)
    {
        _db = db;
        _logger = logger;
        _loc = loc;
    }

    public async Task<string> GenerateOtpAsync(User user, string ipAddress, string purpose = "password_reset")
    {
        var existingOtps = await _db.OtpVerifications
            .Where(o => o.UserId == user.Id && o.Purpose == purpose && !o.IsUsed)
            .ToListAsync();

        foreach (var otp in existingOtps)
            otp.IsUsed = true;

        var code = GenerateSecureCode();
        var codeHash = HashCode(code);

        _db.OtpVerifications.Add(new OtpVerification
        {
            CodeHash = codeHash,
            Email = user.Email,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Purpose = purpose,
            IpAddress = ipAddress
        });

        await _db.SaveChangesAsync();

        _logger.LogInformation("OTP generated for {Email} from IP {Ip}, purpose: {Purpose}",
            user.Email, ipAddress, purpose);

        return code;
    }

    public async Task<string> GenerateOtpAsync(string email, string ipAddress, string purpose, string username, string passwordHash)
    {
        var existingOtps = await _db.OtpVerifications
            .Where(o => o.Email == email && o.Purpose == purpose && !o.IsUsed)
            .ToListAsync();

        foreach (var otp in existingOtps)
            otp.IsUsed = true;

        var code = GenerateSecureCode();
        var codeHash = HashCode(code);

        _db.OtpVerifications.Add(new OtpVerification
        {
            CodeHash = codeHash,
            Email = email,
            Username = username,
            PasswordHash = passwordHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Purpose = purpose,
            IpAddress = ipAddress
        });

        await _db.SaveChangesAsync();

        _logger.LogInformation("OTP generated for {Email} from IP {Ip}, purpose: {Purpose}",
            email, ipAddress, purpose);

        return code;
    }

    public async Task<OtpValidationResult> ValidateOtpAsync(string email, string code, string purpose = "password_reset")
    {
        var otp = await _db.OtpVerifications
            .Where(o =>
                o.Email == email &&
                o.Purpose == purpose &&
                !o.IsUsed)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (otp == null)
        {
            _logger.LogWarning("OTP validation failed for {Email}: no active OTP found", email);
            return new OtpValidationResult { IsValid = false, RemainingAttempts = MaxAttempts };
        }

        if (otp.LockedUntil.HasValue && otp.LockedUntil > DateTime.UtcNow)
        {
            var remaining = (int)(otp.LockedUntil.Value - DateTime.UtcNow).TotalSeconds;
            _logger.LogWarning("OTP validation blocked for {Email}: locked for {Seconds}s", email, remaining);
            return new OtpValidationResult
            {
                IsValid = false,
                IsLocked = true,
                RemainingAttempts = 0
            };
        }

        if (otp.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogWarning("OTP validation failed for {Email}: expired", email);
            return new OtpValidationResult { IsValid = false, RemainingAttempts = MaxAttempts };
        }

        var codeHash = HashCode(code);
        if (otp.CodeHash != codeHash)
        {
            otp.AttemptCount++;

            if (otp.AttemptCount >= MaxAttempts)
            {
                otp.LockedUntil = DateTime.UtcNow.AddMinutes(LockoutMinutes);
                _logger.LogWarning("OTP locked for {Email}: {Attempts} failed attempts", email, otp.AttemptCount);
            }

            await _db.SaveChangesAsync();

            var remaining = Math.Max(0, MaxAttempts - otp.AttemptCount);
            _logger.LogWarning("OTP validation failed for {Email}: wrong code, {Remaining} attempts remaining", email, remaining);

            return new OtpValidationResult
            {
                IsValid = false,
                IsLocked = otp.LockedUntil.HasValue && otp.LockedUntil > DateTime.UtcNow,
                RemainingAttempts = remaining
            };
        }

        otp.IsUsed = true;
        await _db.SaveChangesAsync();

        _logger.LogInformation("OTP validated successfully for {Email}", email);

        return new OtpValidationResult
        {
            IsValid = true,
            RemainingAttempts = MaxAttempts,
            Username = otp.Username,
            PasswordHash = otp.PasswordHash
        };
    }

    public async Task<string> GenerateResetTokenAsync(string email, string ipAddress)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
            throw new InvalidOperationException(_loc[Strings.Auth.UserNotFoundGeneric]);

        var token = GenerateSecureToken();
        var tokenHash = HashCode(token);

        _db.OtpVerifications.Add(new OtpVerification
        {
            CodeHash = tokenHash,
            Email = email,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddMinutes(ResetTokenExpiryMinutes),
            Purpose = "reset_token",
            IpAddress = ipAddress
        });

        await _db.SaveChangesAsync();

        _logger.LogInformation("Reset token generated for {Email} from IP {Ip}", email, ipAddress);

        return token;
    }

    public async Task<bool> ValidateResetTokenAsync(string email, string token)
    {
        var tokenHash = HashCode(token);

        var otp = await _db.OtpVerifications
            .Where(o =>
                o.Email == email &&
                o.Purpose == "reset_token" &&
                !o.IsUsed &&
                o.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (otp == null || otp.CodeHash != tokenHash)
        {
            _logger.LogWarning("Reset token validation failed for {Email}", email);
            return false;
        }

        otp.IsUsed = true;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Reset token validated for {Email}", email);
        return true;
    }

    private static string GenerateSecureCode()
    {
        var bytes = new byte[4];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        var number = BitConverter.ToUInt32(bytes) % 1_000_000;
        return number.ToString("D6");
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string HashCode(string code)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(code));
        return Convert.ToBase64String(bytes);
    }
}
