using System.Text.Json;
using CMS.API.Models;

namespace CMS.API.Middleware;

/// <summary>
/// Global exception handler. Any unhandled exception thrown further down the pipeline (controller
/// or repository) is logged in full server-side and translated into ONE consistent, safe 500 JSON
/// response. Responses that complete normally — including 400 validation, 401 and 403 — pass
/// through untouched, since only thrown exceptions are caught here.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    /// <summary>Generic, client-safe message. Deliberately reveals nothing about the failure.</summary>
    public const string GenericMessage = "An unexpected error occurred.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
            // Full detail (message + stack trace) stays server-side, in the logs.
            _logger.LogError(ex, "Unhandled exception processing {Method} {Path}",
                context.Request.Method, context.Request.Path);

            // If the response has already begun streaming we cannot rewrite it safely.
            if (context.Response.HasStarted)
            {
                throw;
            }

            // Reset only body-shaping headers — leaving CORS headers (already added inbound) intact,
            // so a cross-origin 500 still reaches the browser as a real 500 rather than an opaque
            // network failure. Do NOT Response.Clear(), which would wipe those CORS headers.
            context.Response.Headers.ContentLength = null;
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var payload = JsonSerializer.Serialize(new ErrorResponse(GenericMessage), JsonOptions);
            await context.Response.WriteAsync(payload);
        }
    }
}
