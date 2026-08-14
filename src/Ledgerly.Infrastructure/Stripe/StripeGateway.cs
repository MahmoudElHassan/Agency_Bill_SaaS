using Ledgerly.Application.Abstractions;
using Stripe;
using Stripe.Checkout;
using BillingPortalSessionService = Stripe.BillingPortal.SessionService;
using BillingPortalSessionCreateOptions = Stripe.BillingPortal.SessionCreateOptions;

namespace Ledgerly.Infrastructure.Stripe;

public class StripeGateway : IStripeGateway
{
    private readonly string _secretKey;
    private readonly string _webhookSecret;

    public StripeGateway(string secretKey, string webhookSecret)
    {
        _secretKey = secretKey;
        _webhookSecret = webhookSecret;
        StripeConfiguration.ApiKey = secretKey;
    }

    public string WebhookSecret => _webhookSecret;

    public async Task<StripeCheckoutResult> CreateCheckoutSessionAsync(
        Guid tenantId, string priceId, string successUrl, string cancelUrl, CancellationToken ct = default)
    {
        var service = new SessionService();
        var session = await service.CreateAsync(new SessionCreateOptions
        {
            Mode = "subscription",
            LineItems = new List<SessionLineItemOptions>
            {
                new() { Price = priceId, Quantity = 1 }
            },
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            Metadata = new Dictionary<string, string>
            {
                ["tenantId"] = tenantId.ToString()
            },
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    ["tenantId"] = tenantId.ToString()
                }
            }
        }, cancellationToken: ct);

        return new StripeCheckoutResult(session.Url, session.Id);
    }

    public string CreatePortalSession(string stripeCustomerId, string returnUrl)
    {
        var service = new BillingPortalSessionService();
        var session = service.Create(new BillingPortalSessionCreateOptions
        {
            Customer = stripeCustomerId,
            ReturnUrl = returnUrl
        });
        return session.Url;
    }

    public StripePaymentIntentResult CreatePaymentIntent(long amount, string currency, Guid invoiceId, Guid tenantId)
    {
        var service = new PaymentIntentService();
        var intent = service.Create(new PaymentIntentCreateOptions
        {
            Amount = amount,
            Currency = currency,
            Metadata = new Dictionary<string, string>
            {
                ["invoiceId"] = invoiceId.ToString(),
                ["tenantId"] = tenantId.ToString()
            }
        });
        return new StripePaymentIntentResult(intent.ClientSecret, intent.Id);
    }
}