using Ledgerly.Domain.Entities;
using Ledgerly.Domain.Enums;

namespace Ledgerly.Application.Abstractions;

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task AddAsync(Tenant tenant, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, Guid tenantId, CancellationToken ct = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByIdIgnoringFiltersAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAnyTenantAsync(string email, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IClientRepository
{
    Task<Client?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Client>> ListAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(Client client, CancellationToken ct = default);
    Task UpdateAsync(Client client, CancellationToken ct = default);
    Task DeleteAsync(Client client, CancellationToken ct = default);
    Task<int> CountAsync(Guid tenantId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Invoice?> GetByIdIgnoringFiltersAsync(Guid id, CancellationToken ct = default);
    Task<Invoice?> GetByNumberAsync(string number, Guid tenantId, CancellationToken ct = default);
    Task<Invoice?> GetByPublicTokenAsync(string token, CancellationToken ct = default);
    Task<(IReadOnlyList<Invoice> Items, int Total)> ListAsync(
        Guid tenantId, InvoiceStatus? status, int page, int pageSize, CancellationToken ct = default);
    Task<int> CountInMonthAsync(Guid tenantId, int year, int month, CancellationToken ct = default);
    Task<int> NextSequenceForYearAsync(Guid tenantId, int year, CancellationToken ct = default);
    Task<AddOutcome> AddWithUniqueNumberRetryAsync(Invoice invoice, int maxAttempts, CancellationToken ct = default);
    Task UpdateAsync(Invoice invoice, CancellationToken ct = default);
    Task<IReadOnlyList<Invoice>> ListOverdueDueAsync(DateTime today, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public enum AddOutcome { Created, Failed }

public interface IWebhookEventRepository
{
    Task<bool> ExistsAsync(string stripeEventId, CancellationToken ct = default);
    Task AddAsync(WebhookEvent webhookEvent, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}