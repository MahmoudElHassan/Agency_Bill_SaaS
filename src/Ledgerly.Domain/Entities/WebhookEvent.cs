using Ledgerly.Domain.Common;

namespace Ledgerly.Domain.Entities;

public class WebhookEvent : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StripeEventId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public string? Payload { get; set; }
}