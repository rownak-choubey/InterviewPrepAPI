using InterviewPrepAPI.Configuration;
using InterviewPrepAPI.Extensions;
using InterviewPrepAPI.Localization;
using InterviewPrepAPI.Logging;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFileLogger(Path.Combine(builder.Environment.ContentRootPath, "logs"));

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

app.UseSecurityPipeline();
app.UseAppLocalization();
app.MapAppEndpoints();

app.Run();
