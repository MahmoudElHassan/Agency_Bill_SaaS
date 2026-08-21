using FluentAssertions;
using Ledgerly.Application.Abstractions;
using Ledgerly.Application.Billing;
using Ledgerly.Application.Invoices;
using Ledgerly.Domain.Entities;
using Ledgerly.Domain.Enums;
using Ledgerly.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ledgerly.UnitTests;

public class PublicPayAndWebhookTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly AppDbContext _db;
    private readonly Guid _tenantId = Guid.NewGuid();

    public PublicPayAndWebhookTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_conn)
            .Options;
        _db = new AppDbContext(options, new CurrentTenant { TenantId = _tenantId });
        _db.Database.EnsureCreated();
        _db.Tenants.Add(new Tenant
        {
            Id = _tenantId,
            Name = "Pay Tenant",
            Slug = "pay-" + Guid.NewGuid().ToString("N")[..6],
            Plan = Plan.Free,
            PlanStatus = PlanStatus.Active
        });
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    private Invoice SeedInvoice(InvoiceStatus status, string token)
    {
        var client = new Client
        {
            TenantId = _tenantId,
            Name = "C",
            Email = $"{Guid.NewGuid():N}@x.test",
            Currency = "USD"
        };
        _db.Clients.Add(client);
        var invoice = new Invoice
        {
            TenantId = _tenantId,
            ClientId = client.Id,
            Number = "INV-" + Guid.NewGuid().ToString("N")[..6],
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(-1),
            Currency = "USD",
            Status = status,
            PublicPayToken = token,
            Subtotal = 100,
            Tax = 0,
            Total = 100
        };
        _db.Invoices.Add(invoice);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        return invoice;
    }

    [Fact]
    public async Task PublicPay_allows_Overdue_but_rejects_Draft()
    {
        var overdue = SeedInvoice(InvoiceStatus.Overdue, "tok-overdue");
        var draft = SeedInvoice(InvoiceStatus.Draft, "tok-draft");
        var stripe = new StubStripeGateway();
        var invoices = new InvoiceRepository(_db);

        var overdueResult = await new PublicPayHandler(invoices, stripe).HandleAsync("tok-overdue");
        overdueResult.IsSuccess.Should().BeTrue();

        var draftResult = await new PublicPayHandler(invoices, stripe).HandleAsync("tok-draft");
        draftResult.IsFailure.Should().BeTrue();
        draftResult.Error.Code.Should().Be("invalid_state");
    }

    [Fact]
    public async Task Webhook_checkout_with_priceId_upgrades_plan()
    {
        var handler = CreateWebhookHandler(new StripePriceOptions
        {
            PricePro = "price_pro_test",
            PriceBusiness = "price_biz_test"
        });

        var result = await handler.HandleAsync(new StripeWebhookPayload(
            "evt_checkout_" + Guid.NewGuid().ToString("N"),
            "checkout.session.completed",
            _tenantId,
            InvoiceId: null,
            CustomerId: "cus_1",
            SubscriptionId: "sub_1",
            PriceId: "price_pro_test",
            PaymentIntentId: null));

        result.IsSuccess.Should().BeTrue();
        _db.ChangeTracker.Clear();
        var tenant = await _db.Tenants.FirstAsync(t => t.Id == _tenantId);
        tenant.Plan.Should().Be(Plan.Pro);
        tenant.PlanStatus.Should().Be(PlanStatus.Active);
        tenant.StripeCustomerId.Should().Be("cus_1");
    }

    [Fact]
    public async Task Webhook_does_not_pay_Void_invoice()
    {
        var invoice = SeedInvoice(InvoiceStatus.Void, "tok-void");
        var handler = CreateWebhookHandler(new StripePriceOptions());

        var result = await handler.HandleAsync(new StripeWebhookPayload(
            "evt_pi_" + Guid.NewGuid().ToString("N"),
            "payment_intent.succeeded",
            _tenantId,
            invoice.Id,
            CustomerId: null,
            SubscriptionId: null,
            PriceId: null,
            PaymentIntentId: "pi_1"));

        result.IsSuccess.Should().BeTrue();
        _db.ChangeTracker.Clear();
        var reloaded = await _db.Invoices.IgnoreQueryFilters().FirstAsync(i => i.Id == invoice.Id);
        reloaded.Status.Should().Be(InvoiceStatus.Void);
        reloaded.PaidAt.Should().BeNull();
    }

    [Fact]
    public async Task Webhook_missing_tenant_is_not_marked_processed()
    {
        var handler = CreateWebhookHandler(new StripePriceOptions());
        var eventId = "evt_missing_" + Guid.NewGuid().ToString("N");

        var result = await handler.HandleAsync(new StripeWebhookPayload(
            eventId,
            "checkout.session.completed",
            TenantId: null,
            InvoiceId: null,
            CustomerId: null,
            SubscriptionId: null,
            PriceId: null,
            PaymentIntentId: null));

        result.IsFailure.Should().BeTrue();
        (await _db.WebhookEvents.AnyAsync(e => e.StripeEventId == eventId)).Should().BeFalse();
    }

    private StripeWebhookHandler CreateWebhookHandler(StripePriceOptions prices) =>
        new(
            new WebhookEventRepository(_db),
            new TenantRepository(_db),
            new InvoiceRepository(_db),
            Options.Create(prices),
            NullLogger<StripeWebhookHandler>.Instance);

    private sealed class StubStripeGateway : IStripeGateway
    {
        public Task<StripeCheckoutResult> CreateCheckoutSessionAsync(
            Guid tenantId, string priceId, string successUrl, string cancelUrl, CancellationToken ct = default) =>
            Task.FromResult(new StripeCheckoutResult("https://stripe.test", "cs_test"));

        public string CreatePortalSession(string stripeCustomerId, string returnUrl) => "https://portal.test";

        public StripePaymentIntentResult CreatePaymentIntent(long amount, string currency, Guid invoiceId, Guid tenantId) =>
            new("secret", "pi_stub");
    }
}
