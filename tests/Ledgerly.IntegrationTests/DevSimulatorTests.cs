using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Ledgerly.IntegrationTests;

public class DevSimulatorTests
{
    private static WebApplicationFactory<Program> ProductionFactory()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", "test_test_test_test_test_test_test_test_test_test_test");
        return new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment(Environments.Production);
            b.ConfigureAppConfiguration((_, c) =>
            {
                c.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Hangfire:Disabled"] = "true",
                    ["Database:MigrateOnStartup"] = "false",
                    ["Jwt:Key"] = "test_test_test_test_test_test_test_test_test_test_test"
                });
            });
        });
    }

    private static WebApplicationFactory<Program> DevelopmentFactoryWithSimulatorDisabled()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment(Environments.Development);
            b.ConfigureAppConfiguration((_, c) =>
            {
                c.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Hangfire:Disabled"] = "true",
                    ["Database:MigrateOnStartup"] = "false",
                    ["Dev:EnableWebhookSimulator"] = "false"
                });
            });
        });
    }

    [Fact]
    public async Task Dev_simulator_returns_404_in_production_environment()
    {
        await using var factory = ProductionFactory();
        var client = factory.CreateClient();

        var resp = await client.PostAsync($"/api/dev/webhook/{Guid.NewGuid()}?type=checkout.session.completed", new StringContent(""));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var job = await client.PostAsync("/api/dev/jobs/mark-overdue", new StringContent(""));
        job.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Dev_simulator_returns_404_when_flag_disabled_in_development()
    {
        await using var factory = DevelopmentFactoryWithSimulatorDisabled();
        var client = factory.CreateClient();

        var resp = await client.PostAsync($"/api/dev/webhook/{Guid.NewGuid()}?type=checkout.session.completed", new StringContent(""));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
