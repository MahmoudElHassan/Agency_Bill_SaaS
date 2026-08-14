using Ledgerly.Domain.Common;

namespace Ledgerly.Domain.Entities;

public class User : TenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public Enums.TenantRole Role { get; set; } = Enums.TenantRole.Staff;
    public DateTimeOffset? LastLoginAt { get; set; }

    public Tenant? Tenant { get; set; }
}