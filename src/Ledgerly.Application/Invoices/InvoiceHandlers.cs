using Ledgerly.Application.Abstractions;
using Ledgerly.Domain.Enums;
using Ledgerly.Shared;

namespace Ledgerly.Application.Invoices;

public sealed class UpdateInvoiceHandler
{
    private readonly IInvoiceRepository _invoices;
    private readonly IClientRepository _clients;
    private readonly ICurrentTenant _current;

    public UpdateInvoiceHandler(IInvoiceRepository invoices, IClientRepository clients, ICurrentTenant current)
    {
        _invoices = invoices;
        _clients = clients;
        _current = current;
    }

    public async Task<Result<InvoiceDto>> HandleAsync(Guid id, UpdateInvoiceRequest request, CancellationToken ct = default)
    {
        Guard.AgainstEmptyGuid(id, nameof(id));

        var invoice = await _invoices.GetByIdAsync(id, ct);
        if (invoice is null || invoice.TenantId != _current.TenantId)
            return Result.Failure<InvoiceDto>(Error.NotFound);

        if (invoice.Status != InvoiceStatus.Draft)
            return Result.Failure<InvoiceDto>(Error.InvalidState);

        var client = await _clients.GetByIdAsync(request.ClientId, ct);
        if (client is null || client.TenantId != _current.TenantId)
            return Result.Failure<InvoiceDto>(Error.NotFound);

        invoice.ClientId = client.Id;
        invoice.IssueDate = request.IssueDate;
        invoice.DueDate = request.DueDate;
        invoice.Currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.Trim().ToUpperInvariant();

        invoice.ClearLines();
        foreach (var l in request.Lines ?? Array.Empty<CreateInvoiceLineRequest>())
        {
            invoice.AddLine(new Domain.Entities.InvoiceLine
            {
                Description = l.Description,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                TaxRate = l.TaxRate
            });
        }
        invoice.UpdatedAt = DateTime.UtcNow;

        await _invoices.UpdateAsync(invoice, ct);
        await _invoices.SaveChangesAsync(ct);
        return Result.Success(invoice.ToDto());
    }
}

public sealed class GetInvoiceHandler
{
    private readonly IInvoiceRepository _invoices;
    private readonly ICurrentTenant _current;

    public GetInvoiceHandler(IInvoiceRepository invoices, ICurrentTenant current)
    {
        _invoices = invoices;
        _current = current;
    }

    public async Task<Result<InvoiceDto>> HandleAsync(Guid id, CancellationToken ct = default)
    {
        Guard.AgainstEmptyGuid(id, nameof(id));
        var invoice = await _invoices.GetByIdAsync(id, ct);
        if (invoice is null || invoice.TenantId != _current.TenantId)
            return Result.Failure<InvoiceDto>(Error.NotFound);

        return Result.Success(invoice.ToDto());
    }
}

public sealed class ListInvoicesHandler
{
    private readonly IInvoiceRepository _invoices;
    private readonly ICurrentTenant _current;

    public ListInvoicesHandler(IInvoiceRepository invoices, ICurrentTenant current)
    {
        _invoices = invoices;
        _current = current;
    }

    public async Task<Result<PagedResult<InvoiceDto>>> HandleAsync(InvoiceStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var pagedRequest = new PagedRequest(page, pageSize);
        var (items, total) = await _invoices.ListAsync(_current.TenantId, status, pagedRequest.Page, pagedRequest.Take, ct);
        return Result.Success(new PagedResult<InvoiceDto>(items.Select(i => i.ToDto()).ToList(), total, pagedRequest.Page, pagedRequest.Take));
    }
}

public sealed class SendInvoiceHandler
{
    private readonly IInvoiceRepository _invoices;
    private readonly IEmailSender _email;
    private readonly ICurrentTenant _current;

    public SendInvoiceHandler(IInvoiceRepository invoices, IEmailSender email, ICurrentTenant current)
    {
        _invoices = invoices;
        _email = email;
        _current = current;
    }

    public async Task<Result<InvoiceDto>> HandleAsync(Guid id, CancellationToken ct = default)
    {
        Guard.AgainstEmptyGuid(id, nameof(id));
        var invoice = await _invoices.GetByIdAsync(id, ct);
        if (invoice is null || invoice.TenantId != _current.TenantId)
            return Result.Failure<InvoiceDto>(Error.NotFound);

        if (invoice.Status != InvoiceStatus.Draft)
            return Result.Failure<InvoiceDto>(Error.InvalidState);

        // #region agent log
        try { System.IO.File.AppendAllText("/Users/mhamoud.elhassan10/AI & Projects/VSCode/Ledgerly/.cursor/debug-211c62.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "211c62", hypothesisId = "H1", location = "SendInvoiceHandler.HandleAsync", message = "send transition", data = new { fromStatus = invoice.Status.ToString(), paidWouldBecomeSent = invoice.Status == InvoiceStatus.Paid }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), runId = "post-fix" }) + "\n"); } catch { }
        // #endregion

        invoice.Status = InvoiceStatus.Sent;
        invoice.UpdatedAt = DateTime.UtcNow;
        await _invoices.UpdateAsync(invoice, ct);
        await _invoices.SaveChangesAsync(ct);

        if (invoice.Client is not null)
        {
            await _email.SendAsync(invoice.Client.Email, $"Invoice {invoice.Number}", $"Your invoice {invoice.Number} is ready.", ct);
        }

        return Result.Success(invoice.ToDto());
    }
}

public sealed class VoidInvoiceHandler
{
    private readonly IInvoiceRepository _invoices;
    private readonly ICurrentTenant _current;

    public VoidInvoiceHandler(IInvoiceRepository invoices, ICurrentTenant current)
    {
        _invoices = invoices;
        _current = current;
    }

    public async Task<Result<InvoiceDto>> HandleAsync(Guid id, CancellationToken ct = default)
    {
        Guard.AgainstEmptyGuid(id, nameof(id));
        var invoice = await _invoices.GetByIdAsync(id, ct);
        if (invoice is null || invoice.TenantId != _current.TenantId)
            return Result.Failure<InvoiceDto>(Error.NotFound);

        if (invoice.Status == InvoiceStatus.Paid)
            return Result.Failure<InvoiceDto>(Error.InvalidState);

        invoice.Status = InvoiceStatus.Void;
        invoice.UpdatedAt = DateTime.UtcNow;
        await _invoices.UpdateAsync(invoice, ct);
        await _invoices.SaveChangesAsync(ct);
        return Result.Success(invoice.ToDto());
    }
}