using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Ledgerly.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Ledgerly.Application.Abstractions;
using Ledgerly.Application.Billing;
using Ledgerly.Application.Clients;
using Ledgerly.Application.Invoices;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ledgerly.IntegrationTests;

public class TenantIsolationTests : IClassFixture<HangfireDisabledFactory>
{
    private readonly HangfireDisabledFactory _factory;

    public TenantIsolationTests(HangfireDisabledFactory factory) => _factory = factory;

    [Fact]
    public async Task Cross_tenant_get_returns_404()
    {
        var client = _factory.CreateClient();

        var emailA = $"iso-a-{Guid.NewGuid():N}@ledgerly.test";
        var emailB = $"iso-b-{Guid.NewGuid():N}@ledgerly.test";

        var tokenA = await RegisterAndGetTokenAsync(client, emailA, "Iso Tenant A");
        var tokenB = await RegisterAndGetTokenAsync(client, emailB, "Iso Tenant B");

        var clientId = await CreateClientAsync(client, tokenA, "Acme");
        clientId.Should().NotBe(Guid.Empty);

        var req = new HttpRequestMessage(HttpMethod.Get, $"/api/clients/{clientId}");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenB);
        var resp = await client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Tenant_query_param_does_not_override_jwt()
    {
        var client = _factory.CreateClient();
        var email = $"qstr-{Guid.NewGuid():N}@ledgerly.test";
        var token = await RegisterAndGetTokenAsync(client, email, "Qstr Tenant");
        var clientId = await CreateClientAsync(client, token, "QstrClient");

        var req = new HttpRequestMessage(HttpMethod.Get, $"/api/clients/{clientId}?tenantId={Guid.NewGuid()}");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var resp = await client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<string> RegisterAndGetTokenAsync(HttpClient client, string email, string tenant)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "password123",
            fullName = "User",
            tenantName = $"{tenant} {Guid.NewGuid():N}"
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<AuthBody>>();
        return body!.Data!.AccessToken;
    }

    private static async Task<Guid> CreateClientAsync(HttpClient client, string token, string name)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/clients")
        {
            Content = JsonContent.Create(new { name, email = $"{Guid.NewGuid():N}@x.test", currency = "USD" })
        };
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var resp = await client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<ClientBody>>();
        return body!.Data!.Id;
    }

    private sealed record AuthBody(string AccessToken, string RefreshToken, DateTime ExpiresAt, Guid UserId, Guid TenantId, string Role);
    private sealed record ClientBody(Guid Id, string Name, string Email, string? Address, string Currency, DateTime CreatedAt);
}