using System.Net;
using FluentAssertions;
using Ledgerly.Application.Abstractions;
using Ledgerly.Application.Billing;
using Ledgerly.Domain.Entities;
using Ledgerly.Domain.Enums;
using Ledgerly.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ledgerly.IntegrationTests;

public class WebhookIdempotencyTests : IClassFixture<HangfireDisabledFactory>
{
    private readonly HangfireDisabledFactory _factory;

    public WebhookIdempotencyTests(HangfireDisabledFactory factory) => _factory = factory;

    [Fact]
    public async Task Same_stripe_event_id_marks_invoice_paid_only_once()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant { Id = tenantId, Name = "Webhook Test", Slug = "webhook-" + Guid.NewGuid().ToString("N")[..8], Plan = Plan.Free, PlanStatus = PlanStatus.Active };
        db.Tenants.Add(tenant);
        var client = new Client { TenantId = tenantId, Name = "W Client", Email = $"w-{Guid.NewGuid():N}@x.test", Currency = "USD" };
        db.Clients.Add(client);
        var invoice = new Invoice
        {
            TenantId = tenantId,
            ClientId = client.Id,
            Number = "INV-WH-" + Guid.NewGuid().ToString("N")[..6],
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(14),
            Currency = "USD",
            Status = InvoiceStatus.Sent,
            PublicPayToken = "tok-" + Guid.NewGuid().ToString("N")
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var handler = scope.ServiceProvider.GetRequiredService<StripeWebhookHandler>();
        var eventId = "evt_test_" + Guid.NewGuid().ToString("N");
        var payload = new StripeWebhookPayload(
            eventId,
            "payment_intent.succeeded",
            tenantId,
            invoice.Id,
            CustomerId: "cus_test",
            SubscriptionId: null,
            PriceId: null,
            PaymentIntentId: "pi_test");

        var first = await handler.HandleAsync(payload);
        var second = await handler.HandleAsync(payload);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();

        db.ChangeTracker.Clear();
        var reloaded = await db.Invoices.IgnoreQueryFilters().FirstAsync(i => i.Id == invoice.Id);
        reloaded.Status.Should().Be(InvoiceStatus.Paid);
        reloaded.PaidAt.Should().NotBeNull();
    }
}