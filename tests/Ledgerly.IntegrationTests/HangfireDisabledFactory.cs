using Ledgerly.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Ledgerly.IntegrationTests;

public class HangfireDisabledFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, c) =>
        {
            c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Hangfire:Disabled"] = "true",
                ["Database:MigrateOnStartup"] = "false",
                ["Jwt:Key"] = "test_test_test_test_test_test_test_test_test_test_test"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IRefreshTokenStore>();
            services.AddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>();
        });
    }
}

internal sealed class InMemoryRefreshTokenStore : IRefreshTokenStore
{
    private readonly Dictionary<string, Guid> _map = new();
    private readonly object _gate = new();

    public Task SaveAsync(Guid userId, string token, DateTime expiresAt, CancellationToken ct = default)
    {
        lock (_gate) _map[token] = userId;
        return Task.CompletedTask;
    }

    public Task<Guid?> FindUserIdAsync(string token, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (!_map.TryGetValue(token, out var id))
                return Task.FromResult<Guid?>(null);
            _map.Remove(token); // claim like GETDEL
            return Task.FromResult<Guid?>(id);
        }
    }

    public Task RevokeAsync(string token, CancellationToken ct = default)
    {
        lock (_gate) _map.Remove(token);
        return Task.CompletedTask;
    }
}
