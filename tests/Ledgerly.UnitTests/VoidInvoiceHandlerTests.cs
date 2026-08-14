using FluentAssertions;
using Ledgerly.Application.Abstractions;
using Ledgerly.Application.Invoices;
using Ledgerly.Domain.Entities;
using Ledgerly.Domain.Enums;
using Ledgerly.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ledgerly.UnitTests;

public class VoidInvoiceHandlerTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly AppDbContext _db;
    private readonly Guid _tenantId = Guid.NewGuid();

    public VoidInvoiceHandlerTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_conn)
            .Options;
        _db = new AppDbContext(options, new CurrentTenant { TenantId = _tenantId });
        _db.Database.EnsureCreated();
        _db.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test", Slug = "test-" + Guid.NewGuid().ToString("N")[..6] });
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    [Fact]
    public async Task Cannot_void_a_paid_invoice()
    {
        var client = new Client { TenantId = _tenantId, Name = "C", Email = "c@x.test", Currency = "USD" };
        _db.Clients.Add(client);
        await _db.SaveChangesAsync();

        var invoice = new Invoice
        {
            TenantId = _tenantId,
            Number = "INV-1",
            ClientId = client.Id,
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(14),
            Status = InvoiceStatus.Paid,
            PublicPayToken = "tok"
        };
        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var handler = new VoidInvoiceHandler(new InvoiceRepository(_db), new CurrentTenant { TenantId = _tenantId, IsOwner = true });
        var result = await handler.HandleAsync(invoice.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("invalid_state");
    }

    [Fact]
    public async Task Can_void_a_draft_invoice()
    {
        var client = new Client { TenantId = _tenantId, Name = "C", Email = "c@x.test", Currency = "USD" };
        _db.Clients.Add(client);
        await _db.SaveChangesAsync();

        var invoice = new Invoice
        {
            TenantId = _tenantId,
            Number = "INV-2",
            ClientId = client.Id,
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(14),
            Status = InvoiceStatus.Draft,
            PublicPayToken = "tok2"
        };
        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();

        var handler = new VoidInvoiceHandler(new InvoiceRepository(_db), new CurrentTenant { TenantId = _tenantId, IsOwner = true });
        var result = await handler.HandleAsync(invoice.Id);

        result.IsSuccess.Should().BeTrue();
        _db.ChangeTracker.Clear();
        var reloaded = await _db.Invoices.FirstAsync();
        reloaded.Status.Should().Be(InvoiceStatus.Void);
    }
}