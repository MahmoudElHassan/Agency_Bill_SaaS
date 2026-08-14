using Ledgerly.Api.Middleware;

using Ledgerly.Application.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ledgerly.Api.Controllers;

[ApiController]
[Route("api/billing")]
public class BillingController : ControllerBase
{
    [HttpGet("plans")]
    public async Task<IActionResult> Plans([FromServices] ListPlansHandler handler, [FromServices] IConfiguration config, CancellationToken ct)
    {
        var pro = config["Stripe:PricePro"];
        var biz = config["Stripe:PriceBusiness"];
        var result = await handler.HandleAsync(pro, biz, ct);
        return result.ToActionResult();
    }

    [HttpPost("checkout")]
    [Authorize]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request, [FromServices] CheckoutHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(request, ct);
        return result.ToActionResult();
    }

    [HttpPost("portal")]
    [Authorize]
    public async Task<IActionResult> Portal([FromServices] PortalHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(ct);
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