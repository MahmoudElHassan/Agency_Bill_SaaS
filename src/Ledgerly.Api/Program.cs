using Ledgerly.Api.Middleware;
using Ledgerly.Application.Abstractions;
using Ledgerly.Application.Invoices;
using Ledgerly.Infrastructure;
using Ledgerly.Infrastructure.Persistence;
using Ledgerly.Infrastructure.Stripe;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Stripe;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Ledgerly.Infrastructure.Security;
using Hangfire;
using Hangfire.PostgreSql;
using Hangfire.MemoryStorage;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "Ledgerly API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        [new Microsoft.OpenApi.Models.OpenApiSecurityScheme { Reference = new Microsoft.OpenApi.Models.OpenApiReference { Id = "Bearer", Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme } }] = Array.Empty<string>()
    });
});

var jwtOptions = new JwtOptions
{
    Key = builder.Configuration["Jwt:Key"] ?? string.Empty,
    Issuer = builder.Configuration["Jwt:Issuer"] ?? "ledgerly",
    Audience = builder.Configuration["Jwt:Audience"] ?? "ledgerly"
};

const string WellKnownDevKey = "dev_only_change_me_dev_only_change_me_dev_only_change_me";
if (!builder.Environment.IsDevelopment())
{
    if (string.IsNullOrWhiteSpace(jwtOptions.Key) || jwtOptions.Key.Length < 32 || jwtOptions.Key == WellKnownDevKey)
        throw new InvalidOperationException("Jwt:Key must be set to a non-default value of 32+ characters outside Development.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ClockSkew = TimeSpan.FromMinutes(2),
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddLedgerlyInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ICurrentTenant>(sp =>
{
    var accessor = sp.GetRequiredService<IHttpContextAccessor>();
    return new HttpContextCurrentTenant(accessor);
});

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Default") ?? "Host=localhost;Database=ledgerly;Username=postgres;Password=postgres");

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("Ledgerly.Api"))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddConsoleExporter());

builder.Services.AddCors(o => o.AddPolicy("Dev", p => p
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithOrigins("http://localhost:5173", "http://localhost:3000")));

var disableHangfire = builder.Configuration.GetValue<bool>("Hangfire:Disabled");
if (!disableHangfire)
{
    var useInMemoryHangfire = builder.Environment.IsDevelopment() &&
        builder.Configuration.GetValue<bool>("Hangfire:UseInMemory");

    if (useInMemoryHangfire)
    {
        builder.Services.AddHangfire(c => c.UseMemoryStorage());
    }
    else
    {
        var pgConn = builder.Configuration.GetConnectionString("Default")!;
        builder.Services.AddHangfire(c => c.UsePostgreSqlStorage(pgConn));
    }
    builder.Services.AddHangfireServer();
}

var app = builder.Build();

app.UseMiddleware<ApiExceptionMiddleware>();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Dev");
app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<TenantMiddleware>();

app.MapControllers();
app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment() && !disableHangfire)
{
    app.UseHangfireDashboard("/hangfire");
}

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (!disableHangfire)
{
    RecurringJob.AddOrUpdate<MarkOverdueInvoicesJob>(
        "mark-overdue-hourly",
        job => job.RunAsync(),
        Cron.Hourly);
}

app.Run();

public partial class Program { }

public class MarkOverdueInvoicesJob
{
    private readonly IServiceScopeFactory _scopes;
    public MarkOverdueInvoicesJob(IServiceScopeFactory scopes) => _scopes = scopes;

    public async Task RunAsync()
    {
        using var scope = _scopes.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<MarkOverdueInvoicesHandler>();
        var result = await handler.HandleAsync();
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"mark overdue failed: {result.Error.Code}");
        }
    }
}