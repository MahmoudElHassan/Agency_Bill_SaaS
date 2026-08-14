using System.Text.Json;
using Ledgerly.Shared;
using Microsoft.Extensions.Logging;

namespace Ledgerly.Api.Middleware;

public class ApiExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiExceptionMiddleware> _log;

    public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> log)
    {
        _next = next;
        _log = log;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unhandled exception at {Path}", context.Request.Path);
            if (context.Response.HasStarted) throw;

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            var body = ApiResponse.Fail("internal_error", "An unexpected error occurred.");
            await context.Response.WriteAsync(JsonSerializer.Serialize(body));
        }
    }
}