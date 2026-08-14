using Ledgerly.Shared;

namespace Ledgerly.Application.Abstractions;

public interface ICurrentTenant
{
    Guid TenantId { get; }
    Guid? UserId { get; }
    string? UserEmail { get; }
    bool IsAuthenticated { get; }
    bool IsOwner { get; }
}

public sealed class CurrentTenant : ICurrentTenant
{
    public Guid TenantId { get; init; }
    public Guid? UserId { get; init; }
    public string? UserEmail { get; init; }
    public bool IsAuthenticated => UserId.HasValue;
    public bool IsOwner { get; init; }
}