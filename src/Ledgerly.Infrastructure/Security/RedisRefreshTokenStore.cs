using StackExchange.Redis;
using Ledgerly.Application.Abstractions;

namespace Ledgerly.Infrastructure.Security;

public class RedisRefreshTokenStore : IRefreshTokenStore
{
    private readonly IConnectionMultiplexer _redis;

    public RedisRefreshTokenStore(IConnectionMultiplexer redis) => _redis = redis;

    private static string Key(Guid userId, string token) => $"refresh:{userId}:{token}";

    public async Task SaveAsync(Guid userId, string token, DateTime expiresAt, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var ttl = expiresAt - DateTime.UtcNow;
        if (ttl <= TimeSpan.Zero) return;
        await db.StringSetAsync(Key(userId, token), "1", ttl);
    }

    public async Task<bool> ValidateAsync(Guid userId, string token, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        return await db.KeyExistsAsync(Key(userId, token));
    }

    public async Task RevokeAsync(Guid userId, string token, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(Key(userId, token));
    }
}