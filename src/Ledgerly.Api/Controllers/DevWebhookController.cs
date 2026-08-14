using Ledgerly.Api.Middleware;
using Ledgerly.Application.Billing;
using Ledgerly.Application.Invoices;
using Microsoft.AspNetCore.Mvc;

namespace Ledgerly.Api.Controllers;

[ApiController]
[Route("api/dev")]
public class DevWebhookController : ControllerBase
{
    private readonly StripeWebhookHandler _handler;
    private readonly MarkOverdueInvoicesHandler _overdue;
    private readonly IConfiguration _config;
    private readonly IHostEnvironment _env;

    public DevWebhookController(
        StripeWebhookHandler handler,
        MarkOverdueInvoicesHandler overdue,
        IConfiguration config,
        IHostEnvironment env)
    {
        _handler = handler;
        _overdue = overdue;
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

    [HttpPost("jobs/mark-overdue")]
    public async Task<IActionResult> MarkOverdue(CancellationToken ct)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        var result = await _overdue.HandleAsync(ct);
        return result.ToActionResult();
    }
}