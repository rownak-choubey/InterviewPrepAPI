using System.Text.Json;
using System.Threading.RateLimiting;
using InterviewPrepAPI.Localization;
using InterviewPrepAPI.Models;
using Microsoft.Extensions.Localization;

namespace InterviewPrepAPI.Configuration;

public static class RateLimitingConfiguration
{
    public static IServiceCollection AddCustomRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (context.Request.Method == "OPTIONS")
                    return RateLimitPartition.GetNoLimiter("cors-preflight");

                var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetTokenBucketLimiter(
                    ipAddress,
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 50,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                        TokensPerPeriod = 50,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 10,
                        AutoReplenishment = true
                    });
            });

            options.AddPolicy("forgot-password", context =>
            {
                var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(
                    ipAddress,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(10),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.ContentType = "application/json";
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfterTime)
                    ? ((int?)retryAfterTime.TotalSeconds)?.ToString() ?? "60"
                    : "60";

                context.HttpContext.Response.Headers.RetryAfter = retryAfter;

                var loc = context.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<Strings>>();
                var response = ApiResponse.Fail(
                    loc[Strings.Error.TooManyRequests],
                    StatusCodes.Status429TooManyRequests);

                await context.HttpContext.Response.WriteAsync(
                    JsonSerializer.Serialize(response, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    }),
                    cancellationToken);
            };
        });

        return services;
    }
}
