using Ledgerly.Application.Abstractions;
using Ledgerly.Domain.Enums;
using Ledgerly.Shared;

namespace Ledgerly.Application.Invoices;

public sealed class PublicInvoiceHandler
{
    private readonly IInvoiceRepository _invoices;

    public PublicInvoiceHandler(IInvoiceRepository invoices)
    {
        _invoices = invoices;
    }

    public async Task<Result<PublicInvoiceDto>> HandleAsync(string token, CancellationToken ct = default)
    {
        Guard.AgainstNullOrWhiteSpace(token, nameof(token));
        var invoice = await _invoices.GetByPublicTokenAsync(token, ct);
        if (invoice is null)
            return Result.Failure<PublicInvoiceDto>(Error.NotFound);

        return Result.Success(invoice.ToPublicDto());
    }
}

public sealed class PublicPayHandler
{
    private readonly IInvoiceRepository _invoices;
    private readonly IStripeGateway _stripe;

    public PublicPayHandler(IInvoiceRepository invoices, IStripeGateway stripe)
    {
        _invoices = invoices;
        _stripe = stripe;
    }

    public async Task<Result<PayResponse>> HandleAsync(string token, CancellationToken ct = default)
    {
        Guard.AgainstNullOrWhiteSpace(token, nameof(token));
        var invoice = await _invoices.GetByPublicTokenAsync(token, ct);
        if (invoice is null)
            return Result.Failure<PayResponse>(Error.NotFound);

        if (invoice.Status != InvoiceStatus.Sent)
            return Result.Failure<PayResponse>(Error.InvalidState);

        var amount = (long)decimal.Round(invoice.Total * 100m, 0, MidpointRounding.AwayFromZero);
        var result = _stripe.CreatePaymentIntent(amount, invoice.Currency.ToLowerInvariant(), invoice.Id, invoice.TenantId);
        invoice.StripePaymentIntentId = result.PaymentIntentId;
        invoice.UpdatedAt = DateTime.UtcNow;
        await _invoices.UpdateAsync(invoice, ct);
        await _invoices.SaveChangesAsync(ct);

        return Result.Success(new PayResponse(result.ClientSecret, result.PaymentIntentId));
    }
}