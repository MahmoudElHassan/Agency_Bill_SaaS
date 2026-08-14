using Ledgerly.Application.Abstractions;
using Ledgerly.Domain.Enums;
using Ledgerly.Shared;

namespace Ledgerly.Application.Billing;

public sealed class ListPlansHandler
{
    public Task<Result<IReadOnlyList<PlanDto>>> HandleAsync(string? stripePricePro, string? stripePriceBusiness, CancellationToken ct = default)
    {
        var plans = PlanCatalog.All
            .Select(p => p with
            {
                StripePriceId = p.Code switch
                {
                    "pro" => stripePricePro,
                    "business" => stripePriceBusiness,
                    _ => p.StripePriceId
                }
            })
            .ToList();
        return Task.FromResult(Result.Success<IReadOnlyList<PlanDto>>(plans));
    }
}

public sealed class CheckoutHandler
{
    private readonly IStripeGateway _stripe;
    private readonly ICurrentTenant _current;
    private readonly ITenantRepository _tenants;

    public CheckoutHandler(IStripeGateway stripe, ICurrentTenant current, ITenantRepository tenants)
    {
        _stripe = stripe;
        _current = current;
        _tenants = tenants;
    }

    public async Task<Result<CheckoutResponse>> HandleAsync(CheckoutRequest request, string publicAppUrl, CancellationToken ct = default)
    {
        Guard.AgainstNullOrWhiteSpace(request.PriceId, nameof(request.PriceId));

        var tenant = await _tenants.GetByIdAsync(_current.TenantId, ct);
        if (tenant is null)
            return Result.Failure<CheckoutResponse>(Error.NotFound);

        var successUrl = $"{publicAppUrl.TrimEnd('/')}/billing/success?session_id={{CHECKOUT_SESSION_ID}}";
        var cancelUrl = $"{publicAppUrl.TrimEnd('/')}/billing/cancel";
        var result = await _stripe.CreateCheckoutSessionAsync(tenant.Id, request.PriceId, successUrl, cancelUrl, ct);

        return Result.Success(new CheckoutResponse(result.Url));
    }
}

public sealed class PortalHandler
{
    private readonly IStripeGateway _stripe;
    private readonly ICurrentTenant _current;
    private readonly ITenantRepository _tenants;

    public PortalHandler(IStripeGateway stripe, ICurrentTenant current, ITenantRepository tenants)
    {
        _stripe = stripe;
        _current = current;
        _tenants = tenants;
    }

    public async Task<Result<CheckoutResponse>> HandleAsync(string publicAppUrl, CancellationToken ct = default)
    {
        var tenant = await _tenants.GetByIdAsync(_current.TenantId, ct);
        if (tenant is null)
            return Result.Failure<CheckoutResponse>(Error.NotFound);

        if (string.IsNullOrWhiteSpace(tenant.StripeCustomerId))
            return Result.Failure<CheckoutResponse>(Error.FromMessage("no_stripe_customer", "No Stripe customer for this tenant."));

        var returnUrl = $"{publicAppUrl.TrimEnd('/')}/billing";
        var url = _stripe.CreatePortalSession(tenant.StripeCustomerId, returnUrl);
        return Result.Success(new CheckoutResponse(url));
    }
}

public sealed class BillingStatusHandler
{
    private readonly ICurrentTenant _current;
    private readonly ITenantRepository _tenants;

    public BillingStatusHandler(ICurrentTenant current, ITenantRepository tenants)
    {
        _current = current;
        _tenants = tenants;
    }

    public async Task<Result<BillingStatusResponse>> HandleAsync(CancellationToken ct = default)
    {
        var tenant = await _tenants.GetByIdAsync(_current.TenantId, ct);
        if (tenant is null)
            return Result.Failure<BillingStatusResponse>(Error.NotFound);

        return Result.Success(new BillingStatusResponse(tenant.Plan.ToString(), tenant.PlanStatus.ToString(), tenant.StripeCustomerId, tenant.StripeSubscriptionId));
    }
}