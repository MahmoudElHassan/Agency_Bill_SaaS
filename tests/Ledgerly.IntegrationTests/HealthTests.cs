using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ledgerly.IntegrationTests;

public class HealthTests : IClassFixture<HangfireDisabledFactory>
{
    private readonly HangfireDisabledFactory _factory;

    public HealthTests(HangfireDisabledFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_returns_200()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}