using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Ledgerly.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Ledgerly.IntegrationTests;

public class AuthFlowTests : IClassFixture<HangfireDisabledFactory>
{
    private readonly HangfireDisabledFactory _factory;

    public AuthFlowTests(HangfireDisabledFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_login_me_refresh_logout_round_trip()
    {
        var client = _factory.CreateClient();
        var email = $"auth-{Guid.NewGuid():N}@ledgerly.test";

        var reg = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "password123",
            fullName = "Auth User",
            tenantName = $"Auth Tenant {Guid.NewGuid():N}"
        });
        reg.StatusCode.Should().Be(HttpStatusCode.OK);
        var regBody = await reg.Content.ReadFromJsonAsync<ApiResponse<AuthBody>>();
        regBody!.Success.Should().BeTrue();
        regBody.Data!.AccessToken.Should().NotBeNullOrEmpty();
        regBody.Data.RefreshToken.Should().NotBeNullOrEmpty();
        var initialAccess = regBody.Data.AccessToken;
        var initialRefresh = regBody.Data.RefreshToken;

        var me = await client.GetAsync("/api/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", initialAccess);
        var meAuth = await client.GetAsync("/api/auth/me");
        meAuth.StatusCode.Should().Be(HttpStatusCode.OK);

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = initialRefresh });
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshBody = await refresh.Content.ReadFromJsonAsync<ApiResponse<AuthBody>>();
        refreshBody!.Data!.AccessToken.Should().NotBe(initialAccess);
        refreshBody.Data.RefreshToken.Should().NotBe(initialRefresh);

        var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = initialRefresh });
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var logout = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = refreshBody.Data.RefreshToken });
        logout.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterLogout = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = refreshBody.Data.RefreshToken });
        afterLogout.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed record AuthBody(string AccessToken, string RefreshToken, DateTime ExpiresAt, Guid UserId, Guid TenantId, string Role);
}