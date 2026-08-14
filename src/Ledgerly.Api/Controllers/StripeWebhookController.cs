using Ledgerly.Api.Middleware;
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
    public async Task<IActionResult> Receive(
        [FromServices] StripeGateway gateway,
        [FromServices] StripeWebhookHandler handler,
        CancellationToken ct)
    {
        using var sr = new StreamReader(HttpContext.Request.Body);
        var json = await sr.ReadToEndAsync(ct);

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"], gateway.WebhookSecret);
        }
        catch (StripeException)
        {
            return BadRequest();
        }

        Guid? tenantId = null;
        Guid? invoiceId = null;
        string? customerId = null;
        string? subscriptionId = null;
        string? priceId = null;
        string? paymentIntentId = null;

        switch (stripeEvent.Data.Object)
        {
            case PaymentIntent pi:
                tenantId = ParseGuid(pi.Metadata, "tenantId");
                invoiceId = ParseGuid(pi.Metadata, "invoiceId");
                paymentIntentId = pi.Id;
                break;
            case Stripe.Checkout.Session session:
                tenantId = ParseGuid(session.Metadata, "tenantId");
                customerId = session.CustomerId;
                subscriptionId = session.SubscriptionId;
                break;
            case Subscription sub:
                tenantId = ParseGuid(sub.Metadata, "tenantId");
                customerId = sub.CustomerId;
                subscriptionId = sub.Id;
                priceId = sub.Items?.Data?.FirstOrDefault()?.Price?.Id;
                break;
        }

        var dto = new StripeWebhookPayload(
            stripeEvent.Id,
            stripeEvent.Type,
            tenantId,
            invoiceId,
            customerId,
            subscriptionId,
            priceId,
            paymentIntentId);

        var result = await handler.HandleAsync(dto, ct);
        return result.ToActionResult();
    }

    private static Guid? ParseGuid(IDictionary<string, string>? metadata, string key)
    {
        if (metadata is null) return null;
        if (!metadata.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) return null;
        return Guid.TryParse(raw, out var g) ? g : null;
    }
}