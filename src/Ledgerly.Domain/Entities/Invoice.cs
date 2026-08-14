using Ledgerly.Domain.Common;

namespace Ledgerly.Domain.Entities;

public class Invoice : TenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Number { get; set; } = string.Empty;
    public Guid ClientId { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public Enums.InvoiceStatus Status { get; set; } = Enums.InvoiceStatus.Draft;
    public string Currency { get; set; } = "USD";
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public string? StripePaymentIntentId { get; set; }
    public string PublicPayToken { get; set; } = string.Empty;
    public DateTime? PaidAt { get; set; }

    public Client? Client { get; set; }
    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();

    public void AddLine(InvoiceLine line)
    {
        line.InvoiceId = Id;
        Lines.Add(line);
        RecalculateTotals();
    }

    public void ClearLines()
    {
        Lines.Clear();
    }

    public void RecalculateTotals()
    {
        Subtotal = Lines.Sum(l => l.Quantity * l.UnitPrice);
        Tax = Lines.Sum(l => l.Quantity * l.UnitPrice * l.TaxRate / 100m);
        Total = Subtotal + Tax;
    }
}