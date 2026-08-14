using Ledgerly.Api.Middleware;
using Ledgerly.Application.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ledgerly.Api.Controllers;

[ApiController]
[Route("api/billing")]
public class BillingController : ControllerBase
{
    private readonly IConfiguration _config;

    public BillingController(IConfiguration config) => _config = config;

    [HttpGet("plans")]
    public async Task<IActionResult> Plans([FromServices] ListPlansHandler handler, CancellationToken ct)
    {
        var pro = _config["Stripe:PricePro"];
        var biz = _config["Stripe:PriceBusiness"];
        var result = await handler.HandleAsync(pro, biz, ct);
        return result.ToActionResult();
    }

    [HttpPost("checkout")]
    [Authorize]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request, [FromServices] CheckoutHandler handler, CancellationToken ct)
    {
        var publicAppUrl = _config["PublicAppUrl"] ?? "http://localhost:5173";
        var result = await handler.HandleAsync(request, publicAppUrl, ct);
        return result.ToActionResult();
    }

    [HttpPost("portal")]
    [Authorize]
    public async Task<IActionResult> Portal([FromServices] PortalHandler handler, CancellationToken ct)
    {
        var publicAppUrl = _config["PublicAppUrl"] ?? "http://localhost:5173";
        var result = await handler.HandleAsync(publicAppUrl, ct);
        return result.ToActionResult();
    }

    [HttpGet("status")]
    [Authorize]
    public async Task<IActionResult> Status([FromServices] BillingStatusHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(ct);
        return result.ToActionResult();
    }
}