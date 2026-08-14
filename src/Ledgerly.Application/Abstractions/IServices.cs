using Ledgerly.Shared;

namespace Ledgerly.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface IJwtTokenService
{
    string CreateAccessToken(Guid userId, Guid tenantId, string email, string role);
    (string Token, DateTime ExpiresAt) CreateRefreshToken();
}

public interface IRefreshTokenStore
{
    Task SaveAsync(Guid userId, string token, DateTime expiresAt, CancellationToken ct = default);
    Task<Guid?> FindUserIdAsync(string token, CancellationToken ct = default);
    Task RevokeAsync(string token, CancellationToken ct = default);
}

public interface IStripeGateway
{
    Task<StripeCheckoutResult> CreateCheckoutSessionAsync(
        Guid tenantId, string priceId, string successUrl, string cancelUrl, CancellationToken ct = default);
    string CreatePortalSession(string stripeCustomerId, string returnUrl);
    StripePaymentIntentResult CreatePaymentIntent(long amount, string currency, Guid invoiceId, Guid tenantId);
}

public sealed record StripeCheckoutResult(string Url, string SessionId);
public sealed record StripePaymentIntentResult(string ClientSecret, string PaymentIntentId);

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}

public interface IDateTime
{
    DateTime UtcNow { get; }
}

public sealed class SystemDateTime : IDateTime
{
    public DateTime UtcNow => DateTime.UtcNow;
}