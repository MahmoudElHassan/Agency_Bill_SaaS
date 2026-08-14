using Ledgerly.Api.Middleware;
using Ledgerly.Application.Abstractions;
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
    Key = builder.Configuration["Jwt:Key"] ?? "dev_only_change_me_dev_only_change_me_dev_only_change_me",
    Issuer = builder.Configuration["Jwt:Issuer"] ?? "ledgerly",
    Audience = builder.Configuration["Jwt:Audience"] ?? "ledgerly"
};

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddLedgerlyInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ICurrentTenant>(sp =>
{
    var http = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    if (http is null) return new CurrentTenant { TenantId = Guid.Empty };
    return http.CurrentTenant();
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

var app = builder.Build();

app.UseMiddleware<ApiExceptionMiddleware>();
app.UseSerilogRequestLogging();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("Dev");
app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<TenantMiddleware>();

app.MapControllers();
app.MapHealthChecks("/health");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();

public partial class Program { }