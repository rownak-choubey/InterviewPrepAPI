using InterviewPrepAPI.Endpoints;
using InterviewPrepAPI.Middlewares;
using InterviewPrepAPI.Models;
using Microsoft.AspNetCore.StaticFiles;

namespace InterviewPrepAPI.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseSecurityPipeline(this WebApplication app)
    {
        // TEMP: Diagnostic cookie tracing — remove after root cause identified
        app.UseMiddleware<CookieDiagnosticMiddleware>();

        app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();

        if (!app.Environment.IsProduction())
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }
        app.UseCors("Restricted");

        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                ctx.Context.Response.Headers["Cache-Control"] =
                    ctx.File.Name.EndsWith(".html") ? "no-store" : "public, max-age=31536000";
            }
        });

        app.UseResponseCompression();
        app.UseRateLimiter();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "Interview Prep API v1");
                options.RoutePrefix = "swagger";
            });
        }

        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }

    public static WebApplication MapAppEndpoints(this WebApplication app)
    {
        var configuredProviders = GetConfiguredProviders(app.Configuration);

        app.MapHealthEndpoint();
        app.MapApiRoot(configuredProviders);
        app.MapAuthEndpoints(configuredProviders);
        app.MapDocsPage();

        return app;
    }

    private static void MapHealthEndpoint(this WebApplication app)
    {
        app.MapGet("/health", async (HttpContext context, IServiceProvider services) =>
        {
            try
            {
                var db = services.GetRequiredService<Data.AppDbContext>();
                var canConnect = await db.Database.CanConnectAsync();
                if (!canConnect)
                    return Results.StatusCode(503);

                return Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
            }
            catch
            {
                return Results.StatusCode(503);
            }
        });
    }

    private static void MapApiRoot(this WebApplication app, List<string> providers)
    {
        app.MapGet("/api", () => Results.Ok(ApiResponse<object>.Success(new
        {
            name = "InterviewPrep API",
            version = "v1",
            docs = "/api/docs",
            openapi = "/openapi/v1.json",
            auth = new
            {
                providers,
                endpoints = BuildEndpointMap(providers)
            }
        })));
    }

    private static void MapDocsPage(this WebApplication app)
    {
        app.MapGet("/api/docs", async context =>
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.SendFileAsync(
                Path.Combine(app.Environment.WebRootPath, "docs", "index.html"));
        });
    }

    private static List<string> GetConfiguredProviders(IConfiguration config)
    {
        var providers = new List<string> { "local" };

        if (!string.IsNullOrEmpty(config["Authentication:Google:ClientId"]))
            providers.Add("google");
        if (!string.IsNullOrEmpty(config["Authentication:GitHub:ClientId"]))
            providers.Add("github");

        return providers;
    }

    private static Dictionary<string, string> BuildEndpointMap(List<string> providers)
    {
        var endpoints = new Dictionary<string, string>
        {
            ["register"] = "POST /api/auth/register",
            ["login"] = "POST /api/auth/login",
            ["refresh"] = "POST /api/auth/session/refresh",
            ["logout"] = "POST /api/auth/session/logout",
            ["currentUser"] = "GET /api/auth/session/current-user",
            ["providers"] = "GET /api/auth/oauth/providers"
        };

        if (providers.Contains("google"))
            endpoints["googleLogin"] = "GET /api/auth/oauth/google";
        if (providers.Contains("github"))
            endpoints["githubLogin"] = "GET /api/auth/oauth/github";

        return endpoints;
    }
}
