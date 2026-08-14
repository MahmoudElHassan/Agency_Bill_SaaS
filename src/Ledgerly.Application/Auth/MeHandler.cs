using Ledgerly.Application.Abstractions;
using Ledgerly.Shared;

namespace Ledgerly.Application.Auth;

public sealed class MeHandler
{
    private readonly ICurrentTenant _current;
    private readonly IUserRepository _users;
    private readonly ITenantRepository _tenants;

    public MeHandler(ICurrentTenant current, IUserRepository users, ITenantRepository tenants)
    {
        _current = current;
        _users = users;
        _tenants = tenants;
    }

    public async Task<Result<MeResponse>> HandleAsync(CancellationToken ct = default)
    {
        if (!_current.IsAuthenticated || _current.UserId is null)
            return Result.Failure<MeResponse>(Error.Unauthorized);

        var user = await _users.GetByIdAsync(_current.UserId.Value, ct);
        if (user is null)
            return Result.Failure<MeResponse>(Error.Unauthorized);

        var tenant = await _tenants.GetByIdAsync(user.TenantId, ct);
        if (tenant is null)
            return Result.Failure<MeResponse>(Error.NotFound);

        return Result.Success(new MeResponse(user.Id, user.Email, user.FullName, user.Role.ToString(), tenant.Id, tenant.Name, tenant.Plan.ToString()));
    }
}