using CMS.API.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace CMS.API.Tests;

/// <summary>
/// Unit tests for <see cref="ExceptionHandlingMiddleware"/>: a thrown exception becomes a safe 500
/// (generic message, no leaked details) and is logged, while responses that complete normally —
/// 400 validation, 401, 403, 200 — pass through untouched.
/// </summary>
public class ExceptionHandlingMiddlewareTests
{
    private static async Task<(int Status, string Body, string? ContentType)> InvokeAsync(
        RequestDelegate next, Mock<ILogger<ExceptionHandlingMiddleware>>? logger = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();

        var middleware = new ExceptionHandlingMiddleware(
            next, (logger ?? new Mock<ILogger<ExceptionHandlingMiddleware>>()).Object);
        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        return (context.Response.StatusCode, body, context.Response.ContentType);
    }

    [Fact]
    public async Task ThrownException_ReturnsSafe500_WithoutLeakingDetails()
    {
        // Message intentionally packed with SQL / connection detail that must NOT reach the client.
        const string secret = "SELECT PasswordHash FROM AppUser; Server=.\\SQLEXPRESS;Password=hunter2";
        RequestDelegate next = _ => throw new InvalidOperationException(secret);

        var (status, body, contentType) = await InvokeAsync(next);

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.Equal("application/json", contentType);
        Assert.Contains("An unexpected error occurred.", body);
        // No leakage of exception text, SQL, connection details, type name, or stack frames.
        Assert.DoesNotContain("SELECT", body);
        Assert.DoesNotContain("Password", body);
        Assert.DoesNotContain("SQLEXPRESS", body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain(" at ", body);
    }

    [Fact]
    public async Task ThrownException_LogsFullExceptionServerSide()
    {
        var logger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var thrown = new InvalidOperationException("boom");
        RequestDelegate next = _ => throw thrown;

        await InvokeAsync(next, logger);

        logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            thrown,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task Validation400_PassesThroughUnchanged()
    {
        RequestDelegate next = async ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("{\"error\":\"Description is required.\"}");
        };

        var (status, body, _) = await InvokeAsync(next);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Contains("Description is required.", body);
    }

    [Fact]
    public async Task Unauthorized401_PassesThroughUnchanged()
    {
        RequestDelegate next = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };

        var (status, _, _) = await InvokeAsync(next);

        Assert.Equal(StatusCodes.Status401Unauthorized, status);
    }

    [Fact]
    public async Task Forbidden403_PassesThroughUnchanged()
    {
        RequestDelegate next = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };

        var (status, _, _) = await InvokeAsync(next);

        Assert.Equal(StatusCodes.Status403Forbidden, status);
    }

    [Fact]
    public async Task SuccessfulResponse_PassesThroughUnchanged()
    {
        RequestDelegate next = async ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            await ctx.Response.WriteAsync("{\"ok\":true}");
        };

        var (status, body, _) = await InvokeAsync(next);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Contains("ok", body);
    }
}
