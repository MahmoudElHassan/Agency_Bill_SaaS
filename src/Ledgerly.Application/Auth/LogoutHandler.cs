using Ledgerly.Application.Abstractions;
using Ledgerly.Shared;

namespace Ledgerly.Application.Auth;

public sealed class LogoutHandler
{
    private readonly IRefreshTokenStore _refresh;

    public LogoutHandler(IRefreshTokenStore refresh) => _refresh = refresh;

    public async Task<Result> HandleAsync(string refreshToken, CancellationToken ct = default)
    {
        Guard.AgainstNullOrWhiteSpace(refreshToken, nameof(refreshToken));
        await _refresh.RevokeAsync(refreshToken, ct);
        return Result.Success();
    }
}