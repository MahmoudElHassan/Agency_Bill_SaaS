using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Ledgerly.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Ledgerly.IntegrationTests;

public class PlanLimitTests : IClassFixture<HangfireDisabledFactory>
{
    private readonly HangfireDisabledFactory _factory;

    public PlanLimitTests(HangfireDisabledFactory factory) => _factory = factory;

    [Fact]
    public async Task Fourth_invoice_in_month_returns_402()
    {
        var client = _factory.CreateClient();
        var email = $"plan-{Guid.NewGuid():N}@ledgerly.test";

        var reg = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "password123",
            fullName = "Plan User",
            tenantName = $"Plan Tenant {Guid.NewGuid():N}"
        });
        reg.StatusCode.Should().Be(HttpStatusCode.OK);
        var regBody = await reg.Content.ReadFromJsonAsync<ApiResponse<AuthBody>>();
        var token = regBody!.Data!.AccessToken;

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var clientResp = await client.PostAsJsonAsync("/api/clients", new
        {
            name = "PlanClient",
            email = $"{Guid.NewGuid():N}@x.test",
            currency = "USD"
        });
        clientResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var clientBody = await clientResp.Content.ReadFromJsonAsync<ApiResponse<ClientBody>>();
        var clientId = clientBody!.Data!.Id;

        for (var i = 1; i <= 3; i++)
        {
            var invResp = await client.PostAsJsonAsync("/api/invoices", new
            {
                clientId,
                issueDate = DateTime.UtcNow,
                dueDate = DateTime.UtcNow.AddDays(14),
                currency = "USD",
                lines = new[] { new { description = "x", quantity = 1m, unitPrice = 100m, taxRate = 0m } }
            });
            invResp.StatusCode.Should().Be(HttpStatusCode.OK, $"invoice {i} should be created");
        }

        var fourthResp = await client.PostAsJsonAsync("/api/invoices", new
        {
            clientId,
            issueDate = DateTime.UtcNow,
            dueDate = DateTime.UtcNow.AddDays(14),
            currency = "USD",
            lines = new[] { new { description = "x", quantity = 1m, unitPrice = 100m, taxRate = 0m } }
        });
        fourthResp.StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        var fourthBody = await fourthResp.Content.ReadFromJsonAsync<ApiResponse<object>>();
        fourthBody!.Success.Should().BeFalse();
        fourthBody.Error!.Code.Should().Be("plan_limit");
    }

    private sealed record AuthBody(string AccessToken, string RefreshToken, DateTime ExpiresAt, Guid UserId, Guid TenantId, string Role);
    private sealed record ClientBody(Guid Id, string Name, string Email, string? Address, string Currency, DateTime CreatedAt);
}