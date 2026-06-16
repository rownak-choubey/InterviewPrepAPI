using System.Text;
using InterviewPrepAPI.Configuration;
using InterviewPrepAPI.Data;
using InterviewPrepAPI.Models;
using InterviewPrepAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace InterviewPrepAPI.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            var host = config.GetConnectionString("Host") ?? "localhost";
            var port = config.GetConnectionString("Port") ?? "5432";
            var database = config.GetConnectionString("Database") ?? "interviewprep_db";
            var username = config.GetConnectionString("Username") ?? "interviewprep";
            var password = config.GetConnectionString("Password") ?? "";

            var connectionString = $"Host={host};Port={port};Database={database};Username={username}";
            if (!string.IsNullOrEmpty(password))
                connectionString += $";Password={password}";

            options.UseNpgsql(connectionString);
        });

        return services;
    }

    public static IServiceCollection AddAuth(this IServiceCollection services, IConfiguration config)
    {
        var jwtSettings = config.GetSection("JwtSettings").Get<JwtSettings>()!;
        var key = Encoding.UTF8.GetBytes(jwtSettings.SecretKey);

        services.Configure<JwtSettings>(config.GetSection("JwtSettings"));

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    // JWT validation failed (malformed, expired, bad signature, etc.)
                    // Return 401 instead of letting the exception bubble as 500
                    context.NoResult();
                    return Task.CompletedTask;
                },
                OnTokenValidated = async context =>
                {
                    // Validate that the user has a valid (non-revoked) refresh token via HttpOnly cookie
                    var refreshToken = context.HttpContext.Request.Cookies["refresh_token"];
                    if (string.IsNullOrEmpty(refreshToken))
                    {
                        context.Fail("Missing refresh token");
                        return;
                    }

                    var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                    var storedToken = await db.RefreshTokens
                        .FirstOrDefaultAsync(t => t.Token == refreshToken && !t.IsRevoked);

                    if (storedToken == null || storedToken.ExpiresAt < DateTime.UtcNow)
                        context.Fail("Refresh token expired or revoked");
                }
            };
        });

        // Conditionally register OAuth providers
        var googleId = config["Authentication:Google:ClientId"];
        if (!string.IsNullOrEmpty(googleId))
        {
            services.AddAuthentication().AddGoogle(options =>
            {
                options.ClientId = googleId;
                options.ClientSecret = config["Authentication:Google:ClientSecret"]!;
                options.CallbackPath = "/api/auth/oauth/google/callback";
            });
        }

        var githubId = config["Authentication:GitHub:ClientId"];
        if (!string.IsNullOrEmpty(githubId))
        {
            services.AddAuthentication().AddGitHub(options =>
            {
                options.ClientId = githubId;
                options.ClientSecret = config["Authentication:GitHub:ClientSecret"]!;
                options.CallbackPath = "/api/auth/oauth/github/callback";
                options.Scope.Add("user:email");
            });
        }

        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration config)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("Restricted", policy =>
            {
                var origins = config.GetSection("AllowedOrigins").Get<string[]>() ?? [];
                policy.WithOrigins(origins)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials()
                      .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
            });
        });

        return services;
    }

    public static IServiceCollection AddRequestLimits(this IServiceCollection services)
    {
        services.Configure<KestrelServerOptions>(options =>
        {
            options.Limits.MaxRequestBodySize = 1024 * 1024;
        });

        services.Configure<FormOptions>(options =>
        {
            options.ValueLengthLimit = 1024 * 1024;
            options.MultipartBodyLengthLimit = 1024 * 1024;
        });

        return services;
    }

    public static IServiceCollection AddAppServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddSingleton<IIpCooldownService, InMemoryIpCooldownService>();

        return services;
    }

    public static IServiceCollection AddApiExplorer(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        return services;
    }
}
