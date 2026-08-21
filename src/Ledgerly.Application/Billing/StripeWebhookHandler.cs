using Ledgerly.Application.Abstractions;
using Ledgerly.Domain.Entities;
using Ledgerly.Domain.Enums;
using Ledgerly.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ledgerly.Application.Billing;

public sealed class StripeWebhookHandler
{
    private readonly IWebhookEventRepository _events;
    private readonly ITenantRepository _tenants;
    private readonly IInvoiceRepository _invoices;
    private readonly StripePriceOptions _priceOptions;
    private readonly ILogger<StripeWebhookHandler> _log;

    public StripeWebhookHandler(
        IWebhookEventRepository events,
        ITenantRepository tenants,
        IInvoiceRepository invoices,
        IOptions<StripePriceOptions> priceOptions,
        ILogger<StripeWebhookHandler> log)
    {
        _events = events;
        _tenants = tenants;
        _invoices = invoices;
        _priceOptions = priceOptions.Value;
        _log = log;
    }

    public async Task<Result> HandleAsync(StripeWebhookPayload payload, CancellationToken ct = default)
    {
        if (await _events.ExistsAsync(payload.StripeEventId, ct))
        {
            _log.LogInformation("Stripe webhook already processed: {Id}", payload.StripeEventId);
            return Result.Success();
        }

        var applied = payload.Type switch
        {
            "checkout.session.completed" => await ApplyCheckoutCompletedAsync(payload, ct),
            "customer.subscription.created" => await ApplySubscriptionUpdatedAsync(payload, ct),
            "customer.subscription.updated" => await ApplySubscriptionUpdatedAsync(payload, ct),
            "customer.subscription.deleted" => await ApplySubscriptionDeletedAsync(payload, ct),
            "payment_intent.succeeded" => await ApplyPaymentIntentSucceededAsync(payload, ct),
            _ => true // ignored types still get recorded so Stripe does not retry forever
        };

        if (!applied)
        {
            _log.LogWarning("Stripe webhook {Id} ({Type}) was not applied; not marking processed",
                payload.StripeEventId, payload.Type);
            return Result.Failure(Error.FromMessage("webhook_apply_failed", "Webhook could not be applied."));
        }

        await _events.AddAsync(new WebhookEvent
        {
            StripeEventId = payload.StripeEventId,
            Type = payload.Type,
            ProcessedAt = DateTime.UtcNow
        }, ct);

        try
        {
            await _events.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Duplicate or failed webhook event insert: {Id}", payload.StripeEventId);
        }

        return Result.Success();
    }

    private async Task<bool> ApplyCheckoutCompletedAsync(StripeWebhookPayload p, CancellationToken ct)
    {
        if (!p.TenantId.HasValue)
        {
            _log.LogWarning("checkout.session.completed without tenantId metadata");
            return false;
        }
        var tenant = await _tenants.GetByIdAsync(p.TenantId.Value, ct);
        if (tenant is null) return false;

        if (!string.IsNullOrWhiteSpace(p.CustomerId))
            tenant.StripeCustomerId = p.CustomerId;
        if (!string.IsNullOrWhiteSpace(p.SubscriptionId))
            tenant.StripeSubscriptionId = p.SubscriptionId;

        var planFromPrice = PlanCatalog.FromPriceId(p.PriceId, _priceOptions);
        if (planFromPrice.HasValue)
            tenant.Plan = planFromPrice.Value;

        tenant.PlanStatus = PlanStatus.Active;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _tenants.SaveChangesAsync(ct);
        return true;
    }

    private async Task<bool> ApplySubscriptionUpdatedAsync(StripeWebhookPayload p, CancellationToken ct)
    {
        if (!p.TenantId.HasValue)
        {
            _log.LogWarning("customer.subscription event without tenantId metadata");
            return false;
        }
        var tenant = await _tenants.GetByIdAsync(p.TenantId.Value, ct);
        if (tenant is null) return false;

        var planFromPrice = PlanCatalog.FromPriceId(p.PriceId, _priceOptions);
        if (planFromPrice.HasValue)
            tenant.Plan = planFromPrice.Value;

        tenant.PlanStatus = PlanStatus.Active;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _tenants.SaveChangesAsync(ct);
        return true;
    }

    private async Task<bool> ApplySubscriptionDeletedAsync(StripeWebhookPayload p, CancellationToken ct)
    {
        if (!p.TenantId.HasValue) return false;
        var tenant = await _tenants.GetByIdAsync(p.TenantId.Value, ct);
        if (tenant is null) return false;

        tenant.Plan = Plan.Free;
        tenant.PlanStatus = PlanStatus.Canceled;
        tenant.StripeSubscriptionId = null;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _tenants.SaveChangesAsync(ct);
        return true;
    }

    private async Task<bool> ApplyPaymentIntentSucceededAsync(StripeWebhookPayload p, CancellationToken ct)
    {
        if (!p.InvoiceId.HasValue || !p.TenantId.HasValue)
        {
            _log.LogWarning("payment_intent.succeeded missing invoiceId/tenantId metadata");
            return false;
        }

        var invoice = await _invoices.GetByIdIgnoringFiltersAsync(p.InvoiceId.Value, ct);
        if (invoice is null)
        {
            _log.LogWarning("payment_intent.succeeded invoice {Id} not found", p.InvoiceId);
            return false;
        }
        if (invoice.TenantId != p.TenantId.Value)
        {
            _log.LogWarning("payment_intent.succeeded invoice {Id} tenant mismatch (expected {Expected}, got {Actual})",
                p.InvoiceId, p.TenantId, invoice.TenantId);
            return false;
        }

        if (invoice.Status is not InvoiceStatus.Sent and not InvoiceStatus.Overdue)
        {
            _log.LogWarning("payment_intent.succeeded ignored for invoice {Id} in status {Status}",
                invoice.Id, invoice.Status);
            // Acknowledge Void/Draft/Paid without mutating; only missing tenant/invoice should retry.
            return true;
        }

        invoice.Status = InvoiceStatus.Paid;
        invoice.PaidAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(p.PaymentIntentId))
            invoice.StripePaymentIntentId = p.PaymentIntentId;
        invoice.UpdatedAt = DateTime.UtcNow;
        await _invoices.UpdateAsync(invoice, ct);
        await _invoices.SaveChangesAsync(ct);
        return true;
    }
}
