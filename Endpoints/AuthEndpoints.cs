using System.Security.Claims;
using InterviewPrepAPI.Localization;
using InterviewPrepAPI.Models;
using InterviewPrepAPI.Services;
using Microsoft.Extensions.Localization;

namespace InterviewPrepAPI.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app, List<string> providers)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        // ─── Public ─────────────────────────────────────────
        group.MapPost("/register", async (
            RegisterDto dto,
            IAuthService auth,
            IStringLocalizer<Strings> loc,
            HttpContext context) =>
        {
            await auth.RegisterAsync(dto, context);
            return Results.Ok(ApiResponse.Success(loc[Strings.Auth.RegisterOtpSent]));
        })
        .WithName("Register")
        .WithDescription("Create a new account with email and password (OTP sent for verification)")
        .Produces<ApiResponse>(StatusCodes.Status200OK)
        .Produces<ApiResponse>(StatusCodes.Status409Conflict);

        group.MapPost("/register/verify-email", async (
            VerifyOtpDto dto,
            IAuthService auth,
            IStringLocalizer<Strings> loc,
            HttpContext context) =>
        {
            var result = await auth.VerifyEmailAsync(dto, context);
            return Results.Ok(ApiResponse<AuthResult>.Success(result, loc[Strings.Auth.VerifyEmailSuccess]));
        })
        .WithName("VerifyEmail")
        .WithDescription("Verify email using the OTP code sent during registration")
        .Produces<ApiResponse<AuthResult>>(StatusCodes.Status200OK)
        .Produces<ApiResponse>(StatusCodes.Status400BadRequest)
        .Produces<ApiResponse>(StatusCodes.Status423Locked);

        group.MapPost("/login", async (
            LoginDto dto,
            IAuthService auth,
            IStringLocalizer<Strings> loc,
            HttpContext context) =>
        {
            var result = await auth.LoginAsync(dto, context);
            return Results.Ok(ApiResponse<AuthResult>.Success(result, loc[Strings.Auth.LoginSuccess]));
        })
        .WithName("Login")
        .WithDescription("Sign in with email and password")
        .Produces<ApiResponse<AuthResult>>(StatusCodes.Status200OK)
        .Produces<ApiResponse>(StatusCodes.Status401Unauthorized)
        .Produces<ApiResponse>(StatusCodes.Status406NotAcceptable);

        // ─── Session ────────────────────────────────────────
        var sessionGroup = group.MapGroup("/session")
            .WithTags("Session");

        sessionGroup.MapPost("/refresh", async (
            IAuthService auth,
            IStringLocalizer<Strings> loc,
            HttpContext context) =>
        {
            var result = await auth.RefreshTokensAsync(context);
            return Results.Ok(ApiResponse<AuthResult>.Success(result, loc[Strings.Auth.TokenRefreshed]));
        })
        .WithName("RefreshToken")
        .WithDescription("Get a new access token using refresh token from HttpOnly cookie")
        .Produces<ApiResponse<AuthResult>>(StatusCodes.Status200OK)
        .Produces<ApiResponse>(StatusCodes.Status401Unauthorized);

        sessionGroup.MapPost("/logout", async (
            IAuthService auth,
            IStringLocalizer<Strings> loc,
            HttpContext context) =>
        {
            await auth.RevokeRefreshTokenAsync(context);
            return Results.Ok(ApiResponse.Success(loc[Strings.Auth.LogoutSuccess]));
        })
        .AllowAnonymous()
        .WithName("Logout")
        .WithDescription("Revoke refresh token and clear HttpOnly cookie")
        .Produces<ApiResponse>(StatusCodes.Status200OK);

        sessionGroup.MapGet("/current-user", (ClaimsPrincipal user) =>
        {
            return Results.Ok(ApiResponse<object>.Success(new
            {
                userId = user.FindFirstValue(ClaimTypes.NameIdentifier),
                email = user.FindFirstValue(ClaimTypes.Email),
                username = user.FindFirst("username")?.Value
            }));
        })
        .RequireAuthorization()
        .WithName("GetCurrentUser")
        .WithDescription("Get the currently authenticated user's profile");

        // ─── OAuth ──────────────────────────────────────────
        var oauthGroup = group.MapGroup("/oauth")
            .WithTags("OAuth");

        oauthGroup.MapGet("/providers", () => Results.Ok(ApiResponse<object>.Success(new
        {
            providers = providers.Select(p => new
            {
                name = p,
                enabled = true,
                loginUrl = p == "local" ? null : $"/api/auth/oauth/{p}"
            })
        })))
        .WithName("GetProviders")
        .WithDescription("List available authentication providers")
        .AllowAnonymous();

        if (providers.Contains("google"))
        {
            oauthGroup.MapGet("/google", (HttpContext context) =>
            {
                var properties = new Microsoft.AspNetCore.Authentication.AuthenticationProperties
                {
                    RedirectUri = "/api/auth/oauth/google/callback"
                };
                return Results.Challenge(properties, ["Google"]);
            })
            .WithName("GoogleLogin")
            .WithDescription("Redirect to Google OAuth consent screen");
        }

        if (providers.Contains("github"))
        {
            oauthGroup.MapGet("/github", (HttpContext context) =>
            {
                var properties = new Microsoft.AspNetCore.Authentication.AuthenticationProperties
                {
                    RedirectUri = "/api/auth/oauth/github/callback"
                };
                return Results.Challenge(properties, ["GitHub"]);
            })
            .WithName("GitHubLogin")
            .WithDescription("Redirect to GitHub OAuth consent screen");
        }

        // ─── Password ───────────────────────────────────────
        var passwordGroup = group.MapGroup("/password")
            .WithTags("Password");

        passwordGroup.MapPost("/forgot", async (
            ForgotPasswordDto dto,
            IOtpService otpService,
            IEmailService emailService,
            IAuthService auth,
            IIpCooldownService cooldown,
            IStringLocalizer<Strings> loc,
            HttpContext context) =>
        {
            ClearAuthCookies(context);
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            if (!cooldown.IsAllowed(ip, "forgot-password", 60))
                return Results.Json(
                    ApiResponse.Fail(loc[Strings.Error.TooManyRequests], 429),
                    statusCode: 429);

            var user = await auth.GetUserByEmailAsync(dto.Email);
            if (user == null)
                return Results.Json(
                    ApiResponse.Fail(loc[Strings.Auth.UserNotFound], 404),
                    statusCode: 404);

            var code = await otpService.GenerateOtpAsync(user, ip);
            await emailService.SendOtpEmailAsync(dto.Email, code);
            return Results.Ok(ApiResponse.Success(loc[Strings.Password.ForgotSent]));
        })
        .RequireRateLimiting("forgot-password")
        .WithName("ForgotPassword")
        .WithDescription("Request a password reset OTP via email")
        .Produces<ApiResponse>(StatusCodes.Status200OK)
        .Produces<ApiResponse>(StatusCodes.Status404NotFound)
        .Produces<ApiResponse>(StatusCodes.Status429TooManyRequests);

        passwordGroup.MapPost("/verify-otp", async (
            VerifyOtpDto dto,
            IOtpService otpService,
            IAuthService auth,
            IIpCooldownService cooldown,
            IStringLocalizer<Strings> loc,
            HttpContext context) =>
        {
            ClearAuthCookies(context);
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            if (!cooldown.IsAllowed(ip, "verify-otp", 10))
                return Results.Json(
                    ApiResponse.Fail(loc[Strings.Error.TooManyRequests], 429),
                    statusCode: 429);

            var user = await auth.GetUserByEmailAsync(dto.Email);
            if (user == null)
                return Results.Json(
                    ApiResponse.Fail(loc[Strings.Auth.UserNotFound], 404),
                    statusCode: 404);

            var result = await otpService.ValidateOtpAsync(dto.Email, dto.Code);
            if (!result.IsValid)
            {
                if (result.IsLocked)
                    return Results.Json(
                        ApiResponse.Fail(loc[Strings.Password.VerifyLocked], 423),
                        statusCode: 423);

                return Results.Json(
                    ApiResponse.Fail(loc[Strings.Password.VerifyInvalidOrExpired, result.RemainingAttempts], 400),
                    statusCode: 400);
            }

            var resetToken = await otpService.GenerateResetTokenAsync(dto.Email, ip);
            return Results.Ok(ApiResponse<object>.Success(new
            {
                resetToken,
                expiresIn = 300
            }, loc[Strings.Password.VerifySuccess]));
        })
        .WithName("VerifyOtp")
        .WithDescription("Verify the OTP code sent to your email")
        .Produces<ApiResponse>(StatusCodes.Status200OK)
        .Produces<ApiResponse>(StatusCodes.Status400BadRequest)
        .Produces<ApiResponse>(StatusCodes.Status404NotFound)
        .Produces<ApiResponse>(StatusCodes.Status423Locked);

        passwordGroup.MapPost("/reset", async (
            ResetPasswordDto dto,
            IOtpService otpService,
            IAuthService auth,
            IIpCooldownService cooldown,
            IStringLocalizer<Strings> loc,
            HttpContext context) =>
        {
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Invalidate any existing session
            ClearAuthCookies(context);

            if (!cooldown.IsAllowed(ip, "reset-password", 30))
                return Results.Json(
                    ApiResponse.Fail(loc[Strings.Error.TooManyRequests], 429),
                    statusCode: 429);

            var user = await auth.GetUserByEmailAsync(dto.Email);
            if (user == null)
                return Results.Json(
                    ApiResponse.Fail(loc[Strings.Auth.UserNotFound], 404),
                    statusCode: 404);

            var tokenValid = await otpService.ValidateResetTokenAsync(dto.Email, dto.Code);
            if (!tokenValid)
                return Results.Json(
                    ApiResponse.Fail(loc[Strings.Password.ResetInvalidToken], 400),
                    statusCode: 400);

            await auth.ResetPasswordAsync(dto.Email, dto.NewPassword);
            return Results.Ok(ApiResponse.Success(loc[Strings.Password.ResetSuccess]));
        })
        .WithName("ResetPassword")
        .WithDescription("Set a new password using the reset token from verify-otp")
        .Produces<ApiResponse>(StatusCodes.Status200OK)
        .Produces<ApiResponse>(StatusCodes.Status400BadRequest)
        .Produces<ApiResponse>(StatusCodes.Status404NotFound);
    }

    private static void ClearAuthCookies(HttpContext context)
    {
        context.Response.Cookies.Delete("access_token", new CookieOptions
        {
            Path = "/",
            Secure = true,
            SameSite = SameSiteMode.Strict
        });
        context.Response.Cookies.Delete("refresh_token", new CookieOptions
        {
            Path = "/api/auth",
            Secure = true,
            SameSite = SameSiteMode.Strict
        });
    }
}
