using Ledgerly.Domain.Enums;

namespace Ledgerly.Application.Billing;

public sealed record PlanDto(string Code, string Name, decimal PricePerMonth, string? StripePriceId, IReadOnlyList<string> Features);

public sealed record CheckoutRequest(string PriceId);

public sealed record CheckoutResponse(string Url);

public sealed record BillingStatusResponse(string Plan, string Status, string? StripeCustomerId, string? StripeSubscriptionId);

public sealed record StripeWebhookPayload(
    string StripeEventId,
    string Type,
    Guid? TenantId,
    Guid? InvoiceId,
    string? CustomerId,
    string? SubscriptionId,
    string? PriceId,
    string? PaymentIntentId);

public sealed class StripePriceOptions
{
    public string? PricePro { get; set; }
    public string? PriceBusiness { get; set; }
}

public static class PlanCatalog
{
    public static readonly IReadOnlyList<PlanDto> All = new List<PlanDto>
    {
        new("free", "Free", 0m, null, new[] { "Up to 3 invoices/month", "Manual invoicing" }),
        new("pro", "Pro", 19m, null, new[] { "Unlimited invoices", "Email reminders", "Public pay links" }),
        new("business", "Business", 49m, null, new[] { "Everything in Pro", "Team members", "Stripe Customer Portal" })
    };

    public static Plan FromCode(string code) => code?.ToLowerInvariant() switch
    {
        "free" => Plan.Free,
        "pro" => Plan.Pro,
        "business" => Plan.Business,
        _ => Plan.Free
    };

    public static Plan? FromPriceId(string? priceId, StripePriceOptions opts)
    {
        if (string.IsNullOrWhiteSpace(priceId)) return null;
        if (!string.IsNullOrWhiteSpace(opts.PricePro) && priceId == opts.PricePro) return Plan.Pro;
        if (!string.IsNullOrWhiteSpace(opts.PriceBusiness) && priceId == opts.PriceBusiness) return Plan.Business;
        return null;
    }
}