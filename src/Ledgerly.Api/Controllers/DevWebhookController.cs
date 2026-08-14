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
    private readonly IHostEnvironment _env;

    public DevWebhookController(StripeWebhookHandler handler, IConfiguration config, IHostEnvironment env)
    {
        _handler = handler;
        _config = config;
        _env = env;
    }

    [HttpPost("webhook/{tenantId}")]
    public async Task<IActionResult> Simulate(
        Guid tenantId,
        [FromQuery] string type,
        [FromQuery] Guid? invoiceId,
        [FromQuery] string? customerId,
        [FromQuery] string? subscriptionId,
        [FromQuery] string? priceId,
        [FromQuery] string? paymentIntentId,
        CancellationToken ct)
    {
        if (!_env.IsDevelopment() || !_config.GetValue<bool>("Dev:EnableWebhookSimulator"))
            return NotFound();

        var stripeEventId = "evt_dev_" + Guid.NewGuid().ToString("N");
        var payload = new StripeWebhookPayload(stripeEventId, type, tenantId, invoiceId, customerId, subscriptionId, priceId, paymentIntentId);
        var result = await _handler.HandleAsync(payload, ct);
        return result.ToActionResult();
    }
}