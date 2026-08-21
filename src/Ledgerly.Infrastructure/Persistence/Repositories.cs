using Ledgerly.Application.Abstractions;
using Ledgerly.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ledgerly.Infrastructure.Persistence;

public class TenantRepository : ITenantRepository
{
    private readonly AppDbContext _db;

    public TenantRepository(AppDbContext db) => _db = db;

    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        _db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug, ct);

    public async Task AddAsync(Tenant tenant, CancellationToken ct = default) =>
        await _db.Tenants.AddAsync(tenant, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db) => _db = db;

    public Task<User?> GetByEmailAsync(string email, Guid tenantId, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Email == email, ct);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByIdIgnoringFiltersAsync(Guid id, CancellationToken ct = default) =>
        _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEmailAnyTenantAsync(string email, CancellationToken ct = default) =>
        _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default) =>
        _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct);

    public async Task AddAsync(User user, CancellationToken ct = default) =>
        await _db.Users.AddAsync(user, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}

public class ClientRepository : IClientRepository
{
    private readonly AppDbContext _db;

    public ClientRepository(AppDbContext db) => _db = db;

    public Task<Client?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Clients.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Client>> ListAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = await _db.Clients.Where(c => c.TenantId == tenantId).OrderBy(c => c.Name).ToListAsync(ct);
        return list;
    }

    public async Task AddAsync(Client client, CancellationToken ct = default) =>
        await _db.Clients.AddAsync(client, ct);

    public Task UpdateAsync(Client client, CancellationToken ct = default)
    {
        client.UpdatedAt = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Client client, CancellationToken ct = default)
    {
        _db.Clients.Remove(client);
        return Task.CompletedTask;
    }

    public Task<int> CountAsync(Guid tenantId, CancellationToken ct = default) =>
        _db.Clients.CountAsync(c => c.TenantId == tenantId, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}

public class InvoiceRepository : IInvoiceRepository
{
    private readonly AppDbContext _db;

    public InvoiceRepository(AppDbContext db) => _db = db;

    public Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Invoices.Include(i => i.Lines).Include(i => i.Client).FirstOrDefaultAsync(i => i.Id == id, ct);

    public Task<Invoice?> GetByIdIgnoringFiltersAsync(Guid id, CancellationToken ct = default) =>
        _db.Invoices.IgnoreQueryFilters().Include(i => i.Lines).Include(i => i.Client).FirstOrDefaultAsync(i => i.Id == id, ct);

    public Task<Invoice?> GetByNumberAsync(string number, Guid tenantId, CancellationToken ct = default) =>
        _db.Invoices.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.Number == number && i.TenantId == tenantId, ct);

    public Task<Invoice?> GetByPublicTokenAsync(string token, CancellationToken ct = default) =>
        _db.Invoices.IgnoreQueryFilters().Include(i => i.Lines).Include(i => i.Client).FirstOrDefaultAsync(i => i.PublicPayToken == token, ct);

    public async Task<(IReadOnlyList<Invoice> Items, int Total)> ListAsync(
        Guid tenantId, Domain.Enums.InvoiceStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.Invoices.Include(i => i.Lines).Include(i => i.Client).Where(i => i.TenantId == tenantId);
        if (status.HasValue) q = q.Where(i => i.Status == status.Value);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(i => i.IssueDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public Task<int> CountInMonthAsync(Guid tenantId, int year, int month, CancellationToken ct = default) =>
        _db.Invoices.CountAsync(i =>
            i.TenantId == tenantId &&
            i.IssueDate.Year == year &&
            i.IssueDate.Month == month &&
            i.Status != Domain.Enums.InvoiceStatus.Void, ct);

    public async Task<int> NextSequenceForYearAsync(Guid tenantId, int year, CancellationToken ct = default)
    {
        var prefix = $"INV-{year}-";
        var numbers = await _db.Invoices
            .IgnoreQueryFilters()
            .Where(i => i.TenantId == tenantId && i.Number.StartsWith(prefix))
            .Select(i => i.Number)
            .ToListAsync(ct);

        int max = 0;
        foreach (var n in numbers)
        {
            var tail = n.Substring(prefix.Length);
            if (int.TryParse(tail, out var v) && v > max) max = v;
        }
        return max;
    }

    public async Task AddAsync(Invoice invoice, CancellationToken ct = default) =>
        await _db.Invoices.AddAsync(invoice, ct);

    public async Task<AddOutcome> AddWithUniqueNumberRetryAsync(Invoice invoice, int maxAttempts, CancellationToken ct = default)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            await _db.Invoices.AddAsync(invoice, ct);
            try
            {
                await _db.SaveChangesAsync(ct);
                return AddOutcome.Created;
            }
            catch (DbUpdateException ex) when (
                ex.InnerException?.Message.Contains("IX_Invoices_TenantId_Number") == true ||
                ex.InnerException?.Message.Contains("duplicate key") == true)
            {
                _db.ChangeTracker.Clear();
                var next = await NextSequenceForYearAsync(invoice.TenantId, invoice.IssueDate.Year, ct) + 1;
                invoice.Number = $"INV-{invoice.IssueDate.Year}-{next:0000}";
            }
        }
        return AddOutcome.Failed;
    }

    public Task UpdateAsync(Invoice invoice, CancellationToken ct = default)
    {
        invoice.UpdatedAt = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public Task ResetTracking()
    {
        _db.ChangeTracker.Clear();
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Invoice>> ListOverdueDueAsync(DateTime today, CancellationToken ct = default)
    {
        var list = await _db.Invoices.IgnoreQueryFilters()
            .Include(i => i.Client)
            .Where(i => i.Status == Domain.Enums.InvoiceStatus.Sent && i.DueDate < today)
            .ToListAsync(ct);
        return list;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}

public class WebhookEventRepository : IWebhookEventRepository
{
    private readonly AppDbContext _db;

    public WebhookEventRepository(AppDbContext db) => _db = db;

    public Task<bool> ExistsAsync(string stripeEventId, CancellationToken ct = default) =>
        _db.WebhookEvents.AnyAsync(w => w.StripeEventId == stripeEventId, ct);

    public async Task AddAsync(WebhookEvent webhookEvent, CancellationToken ct = default) =>
        await _db.WebhookEvents.AddAsync(webhookEvent, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}