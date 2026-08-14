using Ledgerly.Domain.Common;

namespace Ledgerly.Domain.Entities;

public class Client : TenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string Currency { get; set; } = "USD";

    public Tenant? Tenant { get; set; }
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}