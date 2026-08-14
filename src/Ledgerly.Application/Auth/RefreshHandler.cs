using Ledgerly.Application.Abstractions;
using Ledgerly.Shared;

namespace Ledgerly.Application.Auth;

public sealed class RefreshHandler
{
    private readonly IRefreshTokenStore _refresh;
    private readonly IUserRepository _users;
    private readonly ITenantRepository _tenants;
    private readonly IJwtTokenService _jwt;

    public RefreshHandler(
        IRefreshTokenStore refresh,
        IUserRepository users,
        ITenantRepository tenants,
        IJwtTokenService jwt)
    {
        _refresh = refresh;
        _users = users;
        _tenants = tenants;
        _jwt = jwt;
    }

    public async Task<Result<AuthResponse>> HandleAsync(RefreshRequest request, CancellationToken ct = default)
    {
        Guard.AgainstNullOrWhiteSpace(request.RefreshToken, nameof(request.RefreshToken));

        var userId = _jwt.GetUserIdFromExpiredToken(request.RefreshToken);
        if (userId is null)
            return Result.Failure<AuthResponse>(Error.Unauthorized);

        var valid = await _refresh.ValidateAsync(userId.Value, request.RefreshToken, ct);
        if (!valid)
            return Result.Failure<AuthResponse>(Error.Unauthorized);

        var user = await _users.GetByIdAsync(userId.Value, ct);
        if (user is null)
            return Result.Failure<AuthResponse>(Error.Unauthorized);

        var tenant = await _tenants.GetByIdAsync(user.TenantId, ct);
        if (tenant is null)
            return Result.Failure<AuthResponse>(Error.FromMessage("tenant_missing", "Tenant not found."));

        await _refresh.RevokeAsync(user.Id, request.RefreshToken, ct);

        var access = _jwt.CreateAccessToken(user.Id, tenant.Id, user.Email, user.Role.ToString());
        var (refresh, expiresAt) = _jwt.CreateRefreshToken();
        await _refresh.SaveAsync(user.Id, refresh, expiresAt, ct);

        return Result.Success(new AuthResponse(access, refresh, expiresAt, user.Id, tenant.Id, user.Role.ToString()));
    }
}