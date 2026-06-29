using InterviewPrepAPI.Configuration;
using InterviewPrepAPI.Extensions;
using InterviewPrepAPI.Localization;
using InterviewPrepAPI.Logging;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFileLogger(Path.Combine(builder.Environment.ContentRootPath, "logs"));

if (builder.Environment.IsProduction())
{
    var vaultId = builder.Configuration["Oci:VaultId"];
    if (!string.IsNullOrEmpty(vaultId))
    {
        builder.Configuration.AddOciSecrets(vaultId, new Dictionary<string, string>
        {
            ["connectionstrings__host"] = "ConnectionStrings:Host",
            ["connectionstrings__port"] = "ConnectionStrings:Port",
            ["connectionstrings__database"] = "ConnectionStrings:Database",
            ["connectionstrings__username"] = "ConnectionStrings:Username",
            ["connectionstrings__password"] = "ConnectionStrings:Password",
            ["jwtsettings__secretkey"] = "JwtSettings:SecretKey",
            ["email__password"] = "Email:Password"
        });
    }
}

builder.Services
    .AddAppLocalization()
    .AddDatabase(builder.Configuration)
    .AddAuth(builder.Configuration)
    .AddCorsPolicy(builder.Configuration)
    .AddCustomRateLimiting()
    .AddResponseCompression()
    .AddRequestLimits()
    .AddAppServices()
    .AddApiExplorer();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InterviewPrepAPI.Data.AppDbContext>();
    db.Database.Migrate();
}

app.UseSecurityPipeline();
app.UseAppLocalization();
app.MapAppEndpoints();

app.Run();
