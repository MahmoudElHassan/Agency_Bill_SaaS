namespace Ledgerly.Domain.Common;

public interface ITenantEntity
{
    Guid TenantId { get; set; }
}