using FluentAssertions;
using Ledgerly.Application.Abstractions;
using Ledgerly.Application.Clients;
using Ledgerly.Domain.Entities;
using Ledgerly.Domain.Enums;
using Ledgerly.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ledgerly.UnitTests;

public class TenantIsolationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();

    public TenantIsolationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new AppDbContext(options, new CurrentTenant { TenantId = _tenantA });
        _db.Database.EnsureCreated();
        _db.Tenants.Add(new Tenant { Id = _tenantA, Name = "A", Slug = "a-" + Guid.NewGuid().ToString("N")[..6] });
        _db.Tenants.Add(new Tenant { Id = _tenantB, Name = "B", Slug = "b-" + Guid.NewGuid().ToString("N")[..6] });
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Client_query_is_filtered_by_tenant()
    {
        _db.Clients.Add(new Client { TenantId = _tenantA, Name = "A Client", Email = "a@x.com" });
        _db.Clients.Add(new Client { TenantId = _tenantB, Name = "B Client", Email = "b@x.com" });
        await _db.SaveChangesAsync();

        var list = await new ClientRepository(_db).ListAsync(_tenantA, default);
        list.Should().HaveCount(1);
        list[0].Name.Should().Be("A Client");
    }

    [Fact]
    public async Task Cross_tenant_get_with_filter_returns_null()
    {
        _db.Clients.Add(new Client { TenantId = _tenantA, Id = Guid.NewGuid(), Name = "A Client", Email = "a@x.com" });
        _db.Clients.Add(new Client { TenantId = _tenantB, Id = Guid.NewGuid(), Name = "B Client", Email = "b@x.com" });
        await _db.SaveChangesAsync();

        var bClientId = (await _db.Clients.IgnoreQueryFilters().FirstAsync(c => c.TenantId == _tenantB)).Id;
        var repo = new ClientRepository(_db);

        var foundWithFilter = await _db.Clients.FirstOrDefaultAsync(c => c.Id == bClientId);
        foundWithFilter.Should().BeNull();
    }
}