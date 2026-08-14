using Ledgerly.Application.Abstractions;
using Ledgerly.Domain.Common;
using Ledgerly.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ledgerly.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    private readonly ICurrentTenant _current;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentTenant current) : base(options)
    {
        _current = current;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tenant>(b =>
        {
            b.HasKey(t => t.Id);
            b.Property(t => t.Name).IsRequired().HasMaxLength(200);
            b.Property(t => t.Slug).IsRequired().HasMaxLength(100);
            b.HasIndex(t => t.Slug).IsUnique();
            b.Property(t => t.Plan).HasConversion<int>();
            b.Property(t => t.PlanStatus).HasConversion<int>();
        });

        modelBuilder.Entity<User>(b =>
        {
            b.HasKey(u => u.Id);
            b.Property(u => u.Email).IsRequired().HasMaxLength(320);
            b.Property(u => u.PasswordHash).IsRequired();
            b.Property(u => u.FullName).IsRequired().HasMaxLength(200);
            b.Property(u => u.Role).HasConversion<int>();
            b.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();
            b.HasOne(u => u.Tenant).WithMany(t => t.Users).HasForeignKey(u => u.TenantId);
            b.HasQueryFilter(u => u.TenantId == _current.TenantId || _current.TenantId == Guid.Empty);
        });

        modelBuilder.Entity<Client>(b =>
        {
            b.HasKey(c => c.Id);
            b.Property(c => c.Name).IsRequired().HasMaxLength(200);
            b.Property(c => c.Email).IsRequired().HasMaxLength(320);
            b.Property(c => c.Address).HasMaxLength(500);
            b.Property(c => c.Currency).IsRequired().HasMaxLength(3);
            b.HasOne(c => c.Tenant).WithMany(t => t.Clients).HasForeignKey(c => c.TenantId);
            b.HasQueryFilter(c => c.TenantId == _current.TenantId || _current.TenantId == Guid.Empty);
        });

        modelBuilder.Entity<Invoice>(b =>
        {
            b.HasKey(i => i.Id);
            b.Property(i => i.Number).IsRequired().HasMaxLength(50);
            b.Property(i => i.Currency).IsRequired().HasMaxLength(3);
            b.Property(i => i.Status).HasConversion<int>();
            b.Property(i => i.PublicPayToken).IsRequired().HasMaxLength(100);
            b.Property(i => i.Subtotal).HasPrecision(18, 2);
            b.Property(i => i.Tax).HasPrecision(18, 2);
            b.Property(i => i.Total).HasPrecision(18, 2);
            b.HasIndex(i => new { i.TenantId, i.Number }).IsUnique();
            b.HasIndex(i => i.PublicPayToken).IsUnique();
            b.HasOne(i => i.Client).WithMany(c => c.Invoices).HasForeignKey(i => i.ClientId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(i => i.Lines).WithOne(l => l.Invoice).HasForeignKey(l => l.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            b.HasQueryFilter(i => i.TenantId == _current.TenantId || _current.TenantId == Guid.Empty);
        });

        modelBuilder.Entity<InvoiceLine>(b =>
        {
            b.HasKey(l => l.Id);
            b.Property(l => l.Description).IsRequired().HasMaxLength(500);
            b.Property(l => l.Quantity).HasPrecision(18, 4);
            b.Property(l => l.UnitPrice).HasPrecision(18, 2);
            b.Property(l => l.TaxRate).HasPrecision(8, 4);
        });

        modelBuilder.Entity<WebhookEvent>(b =>
        {
            b.HasKey(w => w.Id);
            b.Property(w => w.StripeEventId).IsRequired().HasMaxLength(200);
            b.Property(w => w.Type).IsRequired().HasMaxLength(100);
            b.HasIndex(w => w.StripeEventId).IsUnique();
        });
    }
}