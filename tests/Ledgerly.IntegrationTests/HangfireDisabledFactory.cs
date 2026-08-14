using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ledgerly.IntegrationTests;

public class HangfireDisabledFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, c) =>
        {
            c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Hangfire:Disabled"] = "true"
            });
        });
    }
}