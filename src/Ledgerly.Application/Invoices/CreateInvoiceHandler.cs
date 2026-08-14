using System.Security.Cryptography;
using Ledgerly.Application.Abstractions;
using Ledgerly.Domain.Entities;
using Ledgerly.Domain.Enums;
using Ledgerly.Shared;

namespace Ledgerly.Application.Invoices;

public sealed class CreateInvoiceHandler
{
    private readonly IInvoiceRepository _invoices;
    private readonly IClientRepository _clients;
    private readonly ITenantRepository _tenants;
    private readonly ICurrentTenant _current;

    public CreateInvoiceHandler(
        IInvoiceRepository invoices,
        IClientRepository clients,
        ITenantRepository tenants,
        ICurrentTenant current)
    {
        _invoices = invoices;
        _clients = clients;
        _tenants = tenants;
        _current = current;
    }

    public async Task<Result<InvoiceDto>> HandleAsync(CreateInvoiceRequest request, CancellationToken ct = default)
    {
        Guard.AgainstEmptyGuid(request.ClientId, nameof(request.ClientId));
        if (request.Lines is null || request.Lines.Count == 0)
            return Result.Failure<InvoiceDto>(Error.FromMessage("no_lines", "Invoice must have at least one line."));

        var client = await _clients.GetByIdAsync(request.ClientId, ct);
        if (client is null || client.TenantId != _current.TenantId)
            return Result.Failure<InvoiceDto>(Error.NotFound);

        var tenant = await _tenants.GetByIdAsync(_current.TenantId, ct);
        if (tenant is null)
            return Result.Failure<InvoiceDto>(Error.NotFound);

        if (tenant.Plan == Plan.Free)
        {
            var now = DateTime.UtcNow;
            var count = await _invoices.CountInMonthAsync(_current.TenantId, now.Year, now.Month, ct);
            if (count >= 3)
                return Result.Failure<InvoiceDto>(Error.PlanLimit);
        }

        var invoice = new Invoice
        {
            TenantId = _current.TenantId,
            ClientId = client.Id,
            IssueDate = request.IssueDate,
            DueDate = request.DueDate,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.Trim().ToUpperInvariant(),
            Status = InvoiceStatus.Draft,
            PublicPayToken = GenerateToken()
        };

        foreach (var l in request.Lines)
        {
            invoice.AddLine(new InvoiceLine
            {
                Description = l.Description,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                TaxRate = l.TaxRate
            });
        }

        const int maxAttempts = 3;
        var initialNext = await _invoices.NextSequenceForYearAsync(_current.TenantId, invoice.IssueDate.Year, ct) + 1;
        invoice.Number = $"INV-{invoice.IssueDate.Year}-{initialNext:0000}";

        var outcome = await _invoices.AddWithUniqueNumberRetryAsync(invoice, maxAttempts, ct);
        if (outcome == AddOutcome.Created)
            return Result.Success(invoice.ToDto());

        return Result.Failure<InvoiceDto>(Error.FromMessage("number_collision", "Could not allocate a unique invoice number."));
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}