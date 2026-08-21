using System.Text.Json;
using Ledgerly.Shared;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Ledgerly.Api.Middleware;

public class ApiExceptionMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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
        catch (ArgumentException ex)
        {
            _log.LogWarning(ex, "Validation error at {Path}", context.Request.Path);
            if (context.Response.HasStarted) throw;

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            var body = ApiResponse.Fail("validation", ex.Message);
            await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
        }
        catch (StripeException ex)
        {
            _log.LogWarning(ex, "Stripe error at {Path}", context.Request.Path);
            if (context.Response.HasStarted) throw;

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            var body = ApiResponse.Fail("stripe_error", ex.StripeError?.Message ?? "Stripe error");
            await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unhandled exception at {Path}", context.Request.Path);
            if (context.Response.HasStarted) throw;

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            var body = ApiResponse.Fail("internal_error", "An unexpected error occurred.");
            await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
        }
    }
}
