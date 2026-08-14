using Ledgerly.Application.Abstractions;
using Ledgerly.Domain.Enums;
using Ledgerly.Shared;
using Microsoft.Extensions.Logging;

namespace Ledgerly.Application.Invoices;

public sealed class MarkOverdueInvoicesHandler
{
    private readonly IInvoiceRepository _invoices;
    private readonly IEmailSender _email;
    private readonly ICurrentTenant _current;
    private readonly ILogger<MarkOverdueInvoicesHandler> _log;

    public MarkOverdueInvoicesHandler(
        IInvoiceRepository invoices,
        IEmailSender email,
        ICurrentTenant current,
        ILogger<MarkOverdueInvoicesHandler> log)
    {
        _invoices = invoices;
        _email = email;
        _current = current;
        _log = log;
    }

    public async Task<Result<int>> HandleAsync(CancellationToken ct = default)
    {
        if (_current.IsAuthenticated && _current.TenantId != Guid.Empty)
            return Result.Failure<int>(Error.Forbidden);

        var today = DateTime.UtcNow.Date;
        var candidates = await _invoices.ListOverdueDueAsync(today, ct);
        var changed = 0;
        foreach (var inv in candidates)
        {
            if (inv.Status is InvoiceStatus.Draft or InvoiceStatus.Sent)
            {
                inv.Status = InvoiceStatus.Overdue;
                inv.UpdatedAt = DateTime.UtcNow;
                await _invoices.UpdateAsync(invoice: inv, ct);
                changed++;
            }
            if (inv.Client is not null)
            {
                try
                {
                    await _email.SendAsync(inv.Client.Email, $"Reminder: invoice {inv.Number}", $"Your invoice {inv.Number} is past due.", ct);
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Reminder email failed for invoice {Id}", inv.Id);
                }
            }
        }
        await _invoices.SaveChangesAsync(ct);
        return Result.Success(changed);
    }
}