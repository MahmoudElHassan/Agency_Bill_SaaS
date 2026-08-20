using System.Security.Cryptography;
using System.Text;
using StackExchange.Redis;
using Ledgerly.Application.Abstractions;

namespace Ledgerly.Infrastructure.Security;

public class RedisRefreshTokenStore : IRefreshTokenStore
{
    private const string KeyPrefix = "refresh:token:";
    private readonly IConnectionMultiplexer _redis;

    public RedisRefreshTokenStore(IConnectionMultiplexer redis) => _redis = redis;

    private static string Key(string token)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
        return $"{KeyPrefix}{hash}";
    }

    public async Task SaveAsync(Guid userId, string token, DateTime expiresAt, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var ttl = expiresAt - DateTime.UtcNow;
        if (ttl <= TimeSpan.Zero) return;
        await db.StringSetAsync(Key(token), userId.ToString(), ttl);
    }

    public async Task<Guid?> FindUserIdAsync(string token, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var val = await db.StringGetAsync(Key(token));
        if (!val.HasValue) return null;
        return Guid.TryParse(val.ToString(), out var id) ? id : null;
    }

    public async Task RevokeAsync(string token, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(Key(token));
    }
}