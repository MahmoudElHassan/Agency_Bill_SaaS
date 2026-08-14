using Ledgerly.Api.Middleware;
using Ledgerly.Application.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Ledgerly.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, [FromServices] RegisterHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(request, ct);
        return result.ToActionResult();
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, [FromServices] LoginHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(request, ct);
        return result.ToActionResult();
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, [FromServices] RefreshHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(request, ct);
        return result.ToActionResult();
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, [FromServices] LogoutHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(request.RefreshToken, ct);
        return result.ToActionResult();
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me([FromServices] MeHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(ct);
        return result.ToActionResult();
    }
}