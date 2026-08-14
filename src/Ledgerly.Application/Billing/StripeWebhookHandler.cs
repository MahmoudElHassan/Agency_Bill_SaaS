using Ledgerly.Application.Abstractions;
using Ledgerly.Domain.Entities;
using Ledgerly.Domain.Enums;
using Ledgerly.Shared;
using Microsoft.Extensions.Logging;

namespace Ledgerly.Application.Billing;

public sealed class StripeWebhookHandler
{
    private readonly IWebhookEventRepository _events;
    private readonly ITenantRepository _tenants;
    private readonly IInvoiceRepository _invoices;
    private readonly ILogger<StripeWebhookHandler> _log;

    public StripeWebhookHandler(
        IWebhookEventRepository events,
        ITenantRepository tenants,
        IInvoiceRepository invoices,
        ILogger<StripeWebhookHandler> log)
    {
        _events = events;
        _tenants = tenants;
        _invoices = invoices;
        _log = log;
    }

    public async Task<Result> HandleAsync(string stripeEventId, string type, Guid? metaTenantId, Guid? metaInvoiceId, CancellationToken ct = default)
    {
        if (await _events.ExistsAsync(stripeEventId, ct))
        {
            _log.LogInformation("Stripe webhook already processed: {Id}", stripeEventId);
            return Result.Success();
        }

        await _events.AddAsync(new WebhookEvent
        {
            StripeEventId = stripeEventId,
            Type = type,
            ProcessedAt = DateTime.UtcNow
        }, ct);

        try
        {
            await _events.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Duplicate or failed webhook event insert: {Id}", stripeEventId);
            return Result.Success();
        }

        switch (type)
        {
            case "checkout.session.completed":
                if (metaTenantId.HasValue)
                {
                    var tenant = await _tenants.GetByIdAsync(metaTenantId.Value, ct);
                    if (tenant is not null)
                    {
                        tenant.PlanStatus = PlanStatus.Active;
                        tenant.UpdatedAt = DateTime.UtcNow;
                        await _tenants.SaveChangesAsync(ct);
                    }
                }
                break;
            case "customer.subscription.updated":
            case "customer.subscription.deleted":
                if (metaTenantId.HasValue)
                {
                    var tenant = await _tenants.GetByIdAsync(metaTenantId.Value, ct);
                    if (tenant is not null)
                    {
                        tenant.PlanStatus = type.EndsWith("deleted") ? PlanStatus.Canceled : PlanStatus.Active;
                        tenant.UpdatedAt = DateTime.UtcNow;
                        await _tenants.SaveChangesAsync(ct);
                    }
                }
                break;
            case "payment_intent.succeeded":
                if (metaInvoiceId.HasValue)
                {
                    var invoice = await _invoices.GetByIdAsync(metaInvoiceId.Value, ct);
                    if (invoice is not null)
                    {
                        invoice.Status = InvoiceStatus.Paid;
                        invoice.PaidAt = DateTime.UtcNow;
                        invoice.UpdatedAt = DateTime.UtcNow;
                        await _invoices.UpdateAsync(invoice, ct);
                        await _invoices.SaveChangesAsync(ct);
                    }
                }
                break;
            default:
                _log.LogInformation("Ignored Stripe event type: {Type}", type);
                break;
        }

        return Result.Success();
    }
}