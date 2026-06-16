using System.Net;
using System.Text.Json;
using InterviewPrepAPI.Localization;
using InterviewPrepAPI.Models;
using Microsoft.Extensions.Localization;
using Npgsql;

namespace InterviewPrepAPI.Middlewares;

public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var loc = context.RequestServices.GetRequiredService<IStringLocalizer<Strings>>();

        var (statusCode, message) = exception switch
        {
            UnauthorizedAccessException authEx =>
                (HttpStatusCode.Unauthorized, authEx.Message),
            ArgumentException argEx =>
                (HttpStatusCode.BadRequest, argEx.Message),
            InvalidOperationException opEx =>
                (HttpStatusCode.NotAcceptable, opEx.Message),
            KeyNotFoundException =>
                (HttpStatusCode.NotFound, loc[Strings.Error.ResourceNotFound]),
            TimeoutException =>
                (HttpStatusCode.RequestTimeout, loc[Strings.Error.RequestTimedOut]),
            OperationCanceledException =>
                (HttpStatusCode.RequestTimeout, loc[Strings.Error.RequestCancelled]),
            IOException =>
                (HttpStatusCode.ServiceUnavailable, loc[Strings.Error.ServiceUnavailable]),
            NpgsqlException =>
                (HttpStatusCode.ServiceUnavailable, loc[Strings.Error.DatabaseError]),
            _ =>
                (HttpStatusCode.InternalServerError, loc[Strings.Error.Unexpected])
        };

        context.Response.StatusCode = (int)statusCode;

        var response = ApiResponse.Fail(message, (int)statusCode);

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
    }
}
