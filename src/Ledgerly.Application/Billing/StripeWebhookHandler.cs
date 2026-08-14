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

        switch (payload.Type)
        {
            case "checkout.session.completed":
                await ApplyCheckoutCompletedAsync(payload, ct);
                break;
            case "customer.subscription.updated":
                await ApplySubscriptionUpdatedAsync(payload, ct);
                break;
            case "customer.subscription.deleted":
                await ApplySubscriptionDeletedAsync(payload, ct);
                break;
            case "payment_intent.succeeded":
                await ApplyPaymentIntentSucceededAsync(payload, ct);
                break;
            default:
                _log.LogInformation("Ignored Stripe event type: {Type}", payload.Type);
                break;
        }

        // #region agent log
        try { System.IO.File.AppendAllText("/Users/mhamoud.elhassan10/AI & Projects/VSCode/Ledgerly/.cursor/debug-211c62.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "211c62", hypothesisId = "H4", location = "StripeWebhookHandler.HandleAsync", message = "event persisted after apply", data = new { type = payload.Type, hasTenant = payload.TenantId.HasValue, hasInvoice = payload.InvoiceId.HasValue, hasPrice = !string.IsNullOrWhiteSpace(payload.PriceId) }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), runId = "post-fix" }) + "\n"); } catch { }
        // #endregion

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

    private async Task ApplyCheckoutCompletedAsync(StripeWebhookPayload p, CancellationToken ct)
    {
        if (!p.TenantId.HasValue)
        {
            _log.LogWarning("checkout.session.completed without tenantId metadata");
            return;
        }
        var tenant = await _tenants.GetByIdAsync(p.TenantId.Value, ct);
        if (tenant is null) return;

        if (!string.IsNullOrWhiteSpace(p.CustomerId))
            tenant.StripeCustomerId = p.CustomerId;
        if (!string.IsNullOrWhiteSpace(p.SubscriptionId))
            tenant.StripeSubscriptionId = p.SubscriptionId;

        var planFromPrice = PlanCatalog.FromPriceId(p.PriceId, _priceOptions);
        if (planFromPrice.HasValue)
            tenant.Plan = planFromPrice.Value;

        tenant.PlanStatus = PlanStatus.Active;
        tenant.UpdatedAt = DateTime.UtcNow;
        // #region agent log
        try { System.IO.File.AppendAllText("/Users/mhamoud.elhassan10/AI & Projects/VSCode/Ledgerly/.cursor/debug-211c62.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "211c62", hypothesisId = "H3", location = "StripeWebhookHandler.ApplyCheckoutCompletedAsync", message = "checkout completed plan fields", data = new { planBeforeSave = tenant.Plan.ToString(), planStatus = tenant.PlanStatus.ToString(), priceIdPresent = !string.IsNullOrWhiteSpace(p.PriceId), planFieldUnchanged = false }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), runId = "post-fix" }) + "\n"); } catch { }
        // #endregion
        await _tenants.SaveChangesAsync(ct);
    }

    private async Task ApplySubscriptionUpdatedAsync(StripeWebhookPayload p, CancellationToken ct)
    {
        if (!p.TenantId.HasValue)
        {
            _log.LogWarning("customer.subscription.updated without tenantId metadata");
            return;
        }
        var tenant = await _tenants.GetByIdAsync(p.TenantId.Value, ct);
        if (tenant is null) return;

        var planFromPrice = PlanCatalog.FromPriceId(p.PriceId, _priceOptions);
        if (planFromPrice.HasValue)
            tenant.Plan = planFromPrice.Value;

        tenant.PlanStatus = PlanStatus.Active;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _tenants.SaveChangesAsync(ct);
    }

    private async Task ApplySubscriptionDeletedAsync(StripeWebhookPayload p, CancellationToken ct)
    {
        if (!p.TenantId.HasValue) return;
        var tenant = await _tenants.GetByIdAsync(p.TenantId.Value, ct);
        if (tenant is null) return;

        tenant.Plan = Plan.Free;
        tenant.PlanStatus = PlanStatus.Canceled;
        tenant.StripeSubscriptionId = null;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _tenants.SaveChangesAsync(ct);
    }

    private async Task ApplyPaymentIntentSucceededAsync(StripeWebhookPayload p, CancellationToken ct)
    {
        if (!p.InvoiceId.HasValue || !p.TenantId.HasValue)
        {
            _log.LogWarning("payment_intent.succeeded missing invoiceId/tenantId metadata");
            return;
        }

        var invoice = await _invoices.GetByIdIgnoringFiltersAsync(p.InvoiceId.Value, ct);
        if (invoice is null)
        {
            _log.LogWarning("payment_intent.succeeded invoice {Id} not found", p.InvoiceId);
            return;
        }
        if (invoice.TenantId != p.TenantId.Value)
        {
            _log.LogWarning("payment_intent.succeeded invoice {Id} tenant mismatch (expected {Expected}, got {Actual})",
                p.InvoiceId, p.TenantId, invoice.TenantId);
            return;
        }

        invoice.Status = InvoiceStatus.Paid;
        invoice.PaidAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(p.PaymentIntentId))
            invoice.StripePaymentIntentId = p.PaymentIntentId;
        invoice.UpdatedAt = DateTime.UtcNow;
        await _invoices.UpdateAsync(invoice, ct);
        await _invoices.SaveChangesAsync(ct);
    }
}