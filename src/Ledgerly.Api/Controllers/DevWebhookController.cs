using System.Text.Json;
using Ledgerly.Api.Middleware;
using Ledgerly.Application.Billing;
using Microsoft.AspNetCore.Mvc;

namespace Ledgerly.Api.Controllers;

[ApiController]
[Route("api/dev")]
public class DevWebhookController : ControllerBase
{
    private readonly StripeWebhookHandler _handler;
    private readonly IConfiguration _config;

    public DevWebhookController(StripeWebhookHandler handler, IConfiguration config)
    {
        _handler = handler;
        _config = config;
    }

    [HttpPost("webhook/{tenantId}")]
    public async Task<IActionResult> Simulate(Guid tenantId, [FromQuery] string type, [FromQuery] Guid? invoiceId, CancellationToken ct)
    {
        if (!_config.GetValue<bool>("Dev:EnableWebhookSimulator"))
            return NotFound();

        var stripeEventId = "evt_dev_" + Guid.NewGuid().ToString("N");
        var result = await _handler.HandleAsync(stripeEventId, type, tenantId, invoiceId, ct);
        return result.ToActionResult();
    }
}