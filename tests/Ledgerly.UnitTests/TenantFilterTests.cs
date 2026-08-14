using FluentAssertions;
using Ledgerly.Application.Abstractions;
using Ledgerly.Domain.Entities;
using Ledgerly.Domain.Enums;
using Ledgerly.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ledgerly.UnitTests;

public class TenantFilterTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly AppDbContext _db;
    private readonly Guid _realTenant = Guid.NewGuid();

    public TenantFilterTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_conn)
            .Options;
        _db = new AppDbContext(options, new CurrentTenant { TenantId = _realTenant });
        _db.Database.EnsureCreated();
        _db.Tenants.Add(new Tenant { Id = _realTenant, Name = "Real", Slug = "real-" + Guid.NewGuid().ToString("N")[..6] });
        _db.Clients.Add(new Client { TenantId = _realTenant, Name = "Real", Email = "real@x.test" });
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    [Fact]
    public async Task GuidEmpty_tenant_returns_nothing()
    {
        var list = await _db.Clients.ToListAsync();
        list.Should().HaveCount(1);

        var anon = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options,
            new CurrentTenant { TenantId = Guid.Empty });

        var anonList = await anon.Clients.ToListAsync();
        anonList.Should().BeEmpty();
        anon.Dispose();
    }
}