using Ledgerly.Domain.Common;

namespace Ledgerly.Domain.Entities;

public class InvoiceLine : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1m;
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }

    public Invoice? Invoice { get; set; }
}