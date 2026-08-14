using Ledgerly.Domain.Entities;
using Ledgerly.Domain.Enums;

namespace Ledgerly.Application.Invoices;

public sealed record InvoiceLineDto(Guid Id, string Description, decimal Quantity, decimal UnitPrice, decimal TaxRate);
public sealed record InvoiceDto(
    Guid Id, string Number, Guid ClientId, string ClientName,
    DateTime IssueDate, DateTime DueDate, InvoiceStatus Status,
    string Currency, decimal Subtotal, decimal Tax, decimal Total,
    string? StripePaymentIntentId, string PublicPayToken,
    DateTime? PaidAt, IReadOnlyList<InvoiceLineDto> Lines);

public sealed record CreateInvoiceLineRequest(string Description, decimal Quantity, decimal UnitPrice, decimal TaxRate);
public sealed record CreateInvoiceRequest(Guid ClientId, DateTime IssueDate, DateTime DueDate, string Currency, IReadOnlyList<CreateInvoiceLineRequest> Lines);
public sealed record UpdateInvoiceRequest(Guid ClientId, DateTime IssueDate, DateTime DueDate, string Currency, IReadOnlyList<CreateInvoiceLineRequest> Lines);

public sealed record PublicInvoiceDto(
    string Number, string ClientName, string TenantName,
    DateTime IssueDate, DateTime DueDate, InvoiceStatus Status,
    string Currency, decimal Total, IReadOnlyList<InvoiceLineDto> Lines);

public sealed record PayResponse(string ClientSecret, string PaymentIntentId);

public static class InvoiceMapper
{
    public static InvoiceLineDto ToDto(this InvoiceLine l) =>
        new(l.Id, l.Description, l.Quantity, l.UnitPrice, l.TaxRate);

    public static InvoiceDto ToDto(this Invoice i) =>
        new(i.Id, i.Number, i.ClientId, i.Client?.Name ?? string.Empty,
            i.IssueDate, i.DueDate, i.Status, i.Currency,
            i.Subtotal, i.Tax, i.Total,
            i.StripePaymentIntentId, i.PublicPayToken,
            i.PaidAt, i.Lines.Select(l => l.ToDto()).ToList());

    public static PublicInvoiceDto ToPublicDto(this Invoice i) =>
        new(i.Number, i.Client?.Name ?? string.Empty, string.Empty,
            i.IssueDate, i.DueDate, i.Status, i.Currency, i.Total,
            i.Lines.Select(l => l.ToDto()).ToList());
}