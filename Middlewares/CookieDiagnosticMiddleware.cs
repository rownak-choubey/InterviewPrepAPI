namespace InterviewPrepAPI.Middlewares;

/// <summary>
/// TEMPORARY diagnostic middleware to trace Set-Cookie header origins.
/// Logs every Set-Cookie header on every response with full stack trace.
/// Remove after root cause is identified.
/// </summary>
public class CookieDiagnosticMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CookieDiagnosticMiddleware> _logger;

    public CookieDiagnosticMiddleware(RequestDelegate next, ILogger<CookieDiagnosticMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Snapshot response cookies BEFORE the rest of the pipeline
        var requestPath = context.Request.Path.Value ?? "";
        var requestMethod = context.Request.Method;

        await _next(context);

        // After the pipeline completes, check what Set-Cookie headers are being sent
        if (context.Response.Headers.TryGetValue("Set-Cookie", out var setCookieValues))
        {
            var cookieHeaders = setCookieValues.ToArray();
            if (cookieHeaders.Length > 0)
            {
                _logger.LogWarning(
                    "COOKIE_AUDIT | {Method} {Path} | Status: {Status} | Set-Cookie count: {Count}",
                    requestMethod, requestPath, context.Response.StatusCode, cookieHeaders.Length);

                for (int i = 0; i < cookieHeaders.Length; i++)
                {
                    _logger.LogWarning(
                        "COOKIE_AUDIT | {Method} {Path} | Set-Cookie[{Index}]: {Value}",
                        requestMethod, requestPath, i, cookieHeaders[i]);
                }

                // Also log the call stack to identify who added these cookies
                _logger.LogWarning(
                    "COOKIE_AUDIT | {Method} {Path} | Stack trace for cookie origin:",
                    requestMethod, requestPath);
                _logger.LogWarning(Environment.StackTrace);
            }
        }

        // Also log what cookies exist in the request (what the browser sent)
        if (context.Request.Cookies.Count > 0)
        {
            var cookieNames = context.Request.Cookies.Select(c => c.Key).ToList();
            _logger.LogInformation(
                "COOKIE_AUDIT | {Method} {Path} | Request cookies: {Cookies}",
                requestMethod, requestPath, string.Join(", ", cookieNames));
        }
    }
}
