using FluentAssertions;
using Ledgerly.Application.Abstractions;
using Ledgerly.Application.Auth;
using Ledgerly.Domain.Entities;
using Ledgerly.Domain.Enums;
using Ledgerly.Infrastructure.Persistence;
using Ledgerly.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ledgerly.UnitTests;

public class RegisterEmailNormalizeTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly AppDbContext _db;

    public RegisterEmailNormalizeTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_conn)
            .Options;
        _db = new AppDbContext(options, new CurrentTenant());
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    [Fact]
    public async Task Register_rejects_case_variant_of_existing_email()
    {
        var tenants = new TenantRepository(_db);
        var users = new UserRepository(_db);
        var hasher = new BcryptPasswordHasher();
        var jwt = new JwtTokenService(new JwtOptions
        {
            Key = "unit_test_key_unit_test_key_unit_test_12",
            Issuer = "ledgerly",
            Audience = "ledgerly"
        });
        var refresh = new InMemoryRefreshStore();
        var clock = new SystemDateTime();
        var handler = new RegisterHandler(tenants, users, hasher, jwt, refresh, clock);

        var first = await handler.HandleAsync(new RegisterRequest(
            "User@Example.COM", "password123", "Owner", "Acme Co"));
        first.IsSuccess.Should().BeTrue();

        var second = await handler.HandleAsync(new RegisterRequest(
            " user@example.com ", "password123", "Other", "Other Co"));
        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("email_in_use");
    }

    private sealed class InMemoryRefreshStore : IRefreshTokenStore
    {
        private readonly Dictionary<string, Guid> _map = new();

        public Task SaveAsync(Guid userId, string token, DateTime expiresAt, CancellationToken ct = default)
        {
            _map[token] = userId;
            return Task.CompletedTask;
        }

        public Task<Guid?> FindUserIdAsync(string token, CancellationToken ct = default) =>
            Task.FromResult(_map.TryGetValue(token, out var id) ? id : (Guid?)null);

        public Task RevokeAsync(string token, CancellationToken ct = default)
        {
            _map.Remove(token);
            return Task.CompletedTask;
        }
    }
}
