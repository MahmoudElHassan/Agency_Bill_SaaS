using Ledgerly.Domain.Enums;

namespace Ledgerly.Application.Billing;

public sealed record PlanDto(string Code, string Name, decimal PricePerMonth, string? StripePriceId, IReadOnlyList<string> Features);

public sealed record CheckoutRequest(string PriceId);

public sealed record CheckoutResponse(string Url);

public sealed record BillingStatusResponse(string Plan, string Status, string? StripeCustomerId, string? StripeSubscriptionId);

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
}