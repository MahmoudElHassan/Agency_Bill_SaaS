using Ledgerly.Application.Abstractions;
using Ledgerly.Shared;

namespace Ledgerly.Application.Auth;

public sealed class LoginHandler
{
    private readonly IUserRepository _users;
    private readonly ITenantRepository _tenants;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly IRefreshTokenStore _refresh;

    public LoginHandler(
        IUserRepository users,
        ITenantRepository tenants,
        IPasswordHasher hasher,
        IJwtTokenService jwt,
        IRefreshTokenStore refresh)
    {
        _users = users;
        _tenants = tenants;
        _hasher = hasher;
        _jwt = jwt;
        _refresh = refresh;
    }

    public async Task<Result<AuthResponse>> HandleAsync(LoginRequest request, CancellationToken ct = default)
    {
        Guard.AgainstNullOrWhiteSpace(request.Email, nameof(request.Email));
        Guard.AgainstNullOrWhiteSpace(request.Password, nameof(request.Password));

        var user = await _users.GetByEmailAnyTenantAsync(request.Email.Trim().ToLowerInvariant(), ct);
        if (user is null)
            return Result.Failure<AuthResponse>(Error.Unauthorized);

        if (!_hasher.Verify(request.Password, user.PasswordHash))
            return Result.Failure<AuthResponse>(Error.Unauthorized);

        var tenant = await _tenants.GetByIdAsync(user.TenantId, ct);
        if (tenant is null)
            return Result.Failure<AuthResponse>(Error.FromMessage("tenant_missing", "Tenant not found."));

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _users.SaveChangesAsync(ct);

        var access = _jwt.CreateAccessToken(user.Id, tenant.Id, user.Email, user.Role.ToString());
        var (refresh, expiresAt) = _jwt.CreateRefreshToken();
        try
        {
            await _refresh.SaveAsync(user.Id, refresh, expiresAt, ct);
        }
        catch (Exception ex) when (IsSessionStoreFailure(ex))
        {
            return Result.Failure<AuthResponse>(Error.FromMessage(
                "redis_unavailable",
                "Session store unavailable. Check ConnectionStrings:Redis on the API host."));
        }

        return Result.Success(new AuthResponse(access, refresh, expiresAt, user.Id, tenant.Id, user.Role.ToString()));
    }

    private static bool IsSessionStoreFailure(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            var name = e.GetType().Name;
            if (name.Contains("Redis", StringComparison.OrdinalIgnoreCase))
                return true;
            if (e.Message.Contains("Redis", StringComparison.OrdinalIgnoreCase)
                || e.Message.Contains("UnableToConnect", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}