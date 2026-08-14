using Ledgerly.Api.Middleware;

using System.Text.Json;
using Ledgerly.Application.Billing;
using Ledgerly.Infrastructure.Stripe;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace Ledgerly.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/webhooks/stripe")]
public class StripeWebhookController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Receive([FromServices] StripeGateway gateway, [FromServices] StripeWebhookHandler handler, CancellationToken ct)
    {
        using var sr = new StreamReader(HttpContext.Request.Body);
        var json = await sr.ReadToEndAsync(ct);

        Event? stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"], gateway.WebhookSecret);
        }
        catch (StripeException)
        {
            return BadRequest();
        }

        Guid? metaTenantId = null;
        Guid? metaInvoiceId = null;
        if (stripeEvent.Data?.Object is IDictionary<string, object> obj)
        {
            if (obj.TryGetValue("metadata", out var meta) && meta is IDictionary<string, object> dict)
            {
                if (dict.TryGetValue("tenantId", out var t) && Guid.TryParse(t?.ToString(), out var tenantId))
                    metaTenantId = tenantId;
                if (dict.TryGetValue("invoiceId", out var i) && Guid.TryParse(i?.ToString(), out var invoiceId))
                    metaInvoiceId = invoiceId;
            }
        }

        var result = await handler.HandleAsync(stripeEvent.Id, stripeEvent.Type, metaTenantId, metaInvoiceId, ct);
        return result.ToActionResult();
    }
}