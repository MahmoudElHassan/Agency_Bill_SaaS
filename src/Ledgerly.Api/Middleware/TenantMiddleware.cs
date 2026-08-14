using System.Security.Claims;
using Ledgerly.Application.Abstractions;

namespace Ledgerly.Api.Middleware;

public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ICurrentTenant current)
    {
        var tid = context.User.FindFirst("tid")?.Value;
        var sub = context.User.FindFirst("sub")?.Value
                  ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? context.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        var email = context.User.FindFirst("email")?.Value
                    ?? context.User.FindFirst(ClaimTypes.Email)?.Value
                    ?? context.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value;
        var role = context.User.FindFirst("role")?.Value
                   ?? context.User.FindFirst(ClaimTypes.Role)?.Value ?? "Staff";

        var tenantId = Guid.TryParse(tid, out var t) ? t : Guid.Empty;
        var userId = Guid.TryParse(sub, out var u) ? u : (Guid?)null;

        var ctx = new CurrentTenant
        {
            TenantId = tenantId,
            UserId = userId,
            UserEmail = email,
            IsOwner = string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase)
        };
        context.Items[typeof(ICurrentTenant).FullName!] = ctx;

        await _next(context);
    }
}

public static class HttpContextExtensions
{
    public static ICurrentTenant CurrentTenant(this HttpContext ctx)
    {
        var key = typeof(ICurrentTenant).FullName!;
        if (ctx.Items.TryGetValue(key, out var val) && val is ICurrentTenant current) return current;
        return new CurrentTenant { TenantId = Guid.Empty };
    }
}

public sealed class HttpContextCurrentTenant : ICurrentTenant
{
    private readonly IHttpContextAccessor _accessor;
    public HttpContextCurrentTenant(IHttpContextAccessor accessor) => _accessor = accessor;

    private ICurrentTenant Inner => _accessor.HttpContext?.CurrentTenant() ?? new CurrentTenant { TenantId = Guid.Empty };

    public Guid TenantId => Inner.TenantId;
    public Guid? UserId => Inner.UserId;
    public string? UserEmail => Inner.UserEmail;
    public bool IsAuthenticated => Inner.IsAuthenticated;
    public bool IsOwner => Inner.IsOwner;
}