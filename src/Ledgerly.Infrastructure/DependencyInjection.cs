using Ledgerly.Application.Abstractions;
using Ledgerly.Application.Auth;
using Ledgerly.Application.Billing;
using Ledgerly.Application.Clients;
using Ledgerly.Application.Invoices;
using Ledgerly.Infrastructure.Email;
using Ledgerly.Infrastructure.Persistence;
using Ledgerly.Infrastructure.Security;
using Ledgerly.Infrastructure.Stripe;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Ledgerly.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLedgerlyInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connection = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is required");
        services.AddDbContext<AppDbContext>(opts => opts.UseNpgsql(connection));

        var redisConn = config.GetConnectionString("Redis") ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));

        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

        var jwtOptions = new JwtOptions
        {
            Key = config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is required"),
            Issuer = config["Jwt:Issuer"] ?? "ledgerly",
            Audience = config["Jwt:Audience"] ?? "ledgerly",
            AccessTokenMinutes = int.TryParse(config["Jwt:AccessTokenMinutes"], out var m) ? m : 60,
            RefreshTokenDays = int.TryParse(config["Jwt:RefreshTokenDays"], out var d) ? d : 30
        };
        if (jwtOptions.Key.Length < 32)
            throw new InvalidOperationException("Jwt:Key must be at least 32 characters.");
        services.AddSingleton(jwtOptions);
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IRefreshTokenStore, RedisRefreshTokenStore>();

        var stripeSecret = config["Stripe:SecretKey"] ?? "";
        var stripeWebhook = config["Stripe:WebhookSecret"] ?? "";
        services.AddSingleton<IStripeGateway>(_ => new StripeGateway(stripeSecret, stripeWebhook));

        services.AddSingleton<IEmailSender, ConsoleEmailSender>();
        services.AddSingleton<IDateTime, SystemDateTime>();

        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IWebhookEventRepository, WebhookEventRepository>();

        services.AddScoped<RegisterHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<MeHandler>();

        services.AddScoped<CreateClientHandler>();
        services.AddScoped<UpdateClientHandler>();
        services.AddScoped<DeleteClientHandler>();
        services.AddScoped<GetClientHandler>();
        services.AddScoped<ListClientsHandler>();

        services.AddScoped<CreateInvoiceHandler>();
        services.AddScoped<UpdateInvoiceHandler>();
        services.AddScoped<GetInvoiceHandler>();
        services.AddScoped<ListInvoicesHandler>();
        services.AddScoped<SendInvoiceHandler>();
        services.AddScoped<VoidInvoiceHandler>();
        services.AddScoped<PublicInvoiceHandler>();
        services.AddScoped<PublicPayHandler>();

        services.AddScoped<ListPlansHandler>();
        services.AddScoped<CheckoutHandler>();
        services.AddScoped<PortalHandler>();
        services.AddScoped<BillingStatusHandler>();
        services.AddScoped<StripeWebhookHandler>();

        return services;
    }
}