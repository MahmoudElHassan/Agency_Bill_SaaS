using Ledgerly.Application.Abstractions;
using Ledgerly.Domain.Entities;
using Ledgerly.Domain.Enums;
using Ledgerly.Shared;

namespace Ledgerly.Application.Auth;

public sealed class RegisterHandler
{
    private readonly ITenantRepository _tenants;
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly IRefreshTokenStore _refresh;
    private readonly IDateTime _clock;

    public RegisterHandler(
        ITenantRepository tenants,
        IUserRepository users,
        IPasswordHasher hasher,
        IJwtTokenService jwt,
        IRefreshTokenStore refresh,
        IDateTime clock)
    {
        _tenants = tenants;
        _users = users;
        _hasher = hasher;
        _jwt = jwt;
        _refresh = refresh;
        _clock = clock;
    }

    public async Task<Result<AuthResponse>> HandleAsync(RegisterRequest request, CancellationToken ct = default)
    {
        Guard.AgainstNullOrWhiteSpace(request.Email, nameof(request.Email));
        Guard.AgainstNullOrWhiteSpace(request.Password, nameof(request.Password));
        Guard.AgainstNullOrWhiteSpace(request.FullName, nameof(request.FullName));
        Guard.AgainstNullOrWhiteSpace(request.TenantName, nameof(request.TenantName));

        if (request.Password.Length < 8)
            return Result.Failure<AuthResponse>(Error.FromMessage("weak_password", "Password must be at least 8 characters."));

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _users.EmailExistsAsync(email, ct))
            return Result.Failure<AuthResponse>(Error.FromMessage("email_in_use", "Email already registered."));

        var slug = await GenerateUniqueSlugAsync(request.TenantName, ct);

        var tenant = new Tenant
        {
            Name = request.TenantName.Trim(),
            Slug = slug,
            Plan = Plan.Free,
            PlanStatus = PlanStatus.Active
        };
        await _tenants.AddAsync(tenant, ct);

        var user = new User
        {
            TenantId = tenant.Id,
            Email = email,
            PasswordHash = _hasher.Hash(request.Password),
            FullName = request.FullName.Trim(),
            Role = TenantRole.Owner
        };

        try
        {
            await _users.AddAsync(user, ct);
            await _tenants.SaveChangesAsync(ct);
            await _users.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            return Result.Failure<AuthResponse>(Error.FromMessage("email_in_use", "Email already registered."));
        }

        var access = _jwt.CreateAccessToken(user.Id, tenant.Id, user.Email, user.Role.ToString());
        var (refresh, expiresAt) = _jwt.CreateRefreshToken();
        await _refresh.SaveAsync(user.Id, refresh, expiresAt, ct);

        return Result.Success(new AuthResponse(access, refresh, expiresAt, user.Id, tenant.Id, user.Role.ToString()));
    }

    private static bool IsUniqueViolation(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            var msg = e.Message;
            if (msg.Contains("unique", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("IX_Users_Email", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private async Task<string> GenerateUniqueSlugAsync(string name, CancellationToken ct)
    {
        var baseSlug = new string(name.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray())
            .Trim('-');
        while (baseSlug.Contains("--"))
            baseSlug = baseSlug.Replace("--", "-");
        if (string.IsNullOrWhiteSpace(baseSlug))
            baseSlug = "tenant";

        var slug = baseSlug;
        var i = 1;
        while (await _tenants.GetBySlugAsync(slug, ct) is not null)
        {
            i++;
            slug = $"{baseSlug}-{i}";
        }
        return slug;
    }
}