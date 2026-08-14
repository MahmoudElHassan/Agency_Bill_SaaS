using Ledgerly.Domain.Common;

namespace Ledgerly.Domain.Entities;

public class Tenant : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public Enums.Plan Plan { get; set; } = Enums.Plan.Free;
    public Enums.PlanStatus PlanStatus { get; set; } = Enums.PlanStatus.Inactive;

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Client> Clients { get; set; } = new List<Client>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}