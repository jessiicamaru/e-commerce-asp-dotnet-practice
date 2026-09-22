using Ecommerce.Shared.Localization;
using Ecommerce.Shared.Money;
using Ecommerce.Shared.Observability;
using Ecommerce.Cart.Application;
using Ecommerce.Cart.Infrastructure;
using Ecommerce.Cart.Infrastructure.Persistence;
using Ecommerce.Cart.WebApi.Consumers;
using Ecommerce.Cart.WebApi.Grpc;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Middlewares;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Load .env file at startup if it exists (searching upward recursively)
var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
string? dotenv = null;
while (directory != null)
{
    var path = Path.Combine(directory.FullName, ".env");
    if (File.Exists(path))
    {
        dotenv = path;
        break;
    }
    directory = directory.Parent;
}

if (!string.IsNullOrEmpty(dotenv))
{
    foreach (var line in File.ReadAllLines(dotenv))
    {
        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
        {
            continue;
        }

        var parts = trimmed.Split('=', 2);
        if (parts.Length == 2)
        {
            var key = parts[0].Trim();
            var value = parts[1].Trim();

            // Fall back, never override: a variable already set in the real environment wins.
            if (Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}

var envJwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
if (!string.IsNullOrEmpty(envJwtSecret))
{
    builder.Configuration["JwtSettings:Secret"] = envJwtSecret;
}

// Cart database (Port 5439)
var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "123456";
var dbName = Environment.GetEnvironmentVariable("CART_DB_NAME") ?? "ecommerce_cart_db";
var dbPort = Environment.GetEnvironmentVariable("CART_DB_PORT") ?? "5439";
var dbHost = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";

builder.Configuration["ConnectionStrings:DefaultConnection"] =
    $"Host={dbHost};Database={dbName};Username={dbUser};Password={dbPassword};Port={dbPort}";

// TWO endpoints: HTTP/1.1 for REST, HTTP/2 for gRPC - declared TOGETHER, which is not a style choice.
//
// Calling ListenAnyIP at all REPLACES ASPNETCORE_URLS rather than adding to it. Feature 009 learned
// that on Catalog: configuring only the gRPC endpoint silently unbound REST and the container went
// unhealthy. So the HTTP port is declared here too, derived from ASPNETCORE_URLS - localhost on the
// pinned port under start-dev, all interfaces when a container sets it - and there is no app.Run(url)
// fallback at the end, because that would be a third opinion about where to listen.
//
// One plaintext port cannot serve both protocols (telling them apart needs ALPN, which is part of
// TLS); verified on .NET 10 that Http1AndHttp2 on a plaintext endpoint does not serve h2c.
var listenUrlsAtStartup = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
var inContainer = !string.IsNullOrWhiteSpace(listenUrlsAtStartup);

var httpPort = PortFrom(listenUrlsAtStartup) ?? 5062;
var grpcPort = int.TryParse(Environment.GetEnvironmentVariable("CART_GRPC_PORT"), out var parsedGrpc)
    ? parsedGrpc
    : 5162;

builder.WebHost.ConfigureKestrel(kestrel =>
{
    if (inContainer)
    {
        kestrel.ListenAnyIP(httpPort, e => e.Protocols = HttpProtocols.Http1);
        kestrel.ListenAnyIP(grpcPort, e => e.Protocols = HttpProtocols.Http2);
    }
    else
    {
        kestrel.ListenLocalhost(httpPort, e => e.Protocols = HttpProtocols.Http1);
        kestrel.ListenLocalhost(grpcPort, e => e.Protocols = HttpProtocols.Http2);
    }
});

static int? PortFrom(string? urls)
{
    if (string.IsNullOrWhiteSpace(urls))
    {
        return null;
    }

    var first = urls.Split(';', StringSplitOptions.RemoveEmptyEntries)[0];
    var lastColon = first.LastIndexOf(':');

    return lastColon >= 0 && int.TryParse(first[(lastColon + 1)..].TrimEnd('/'), out var port)
        ? port
        : null;
}

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);

// A cart shows product names and option values, which are Catalog's text - so it has to ask for them
// in the language this request is in (specs/021).
builder.Services.AddRequestLanguage(builder.Configuration);

// Which currency the amounts in a response are in (specs/022). Separate from the language on
// purpose: a Vietnamese person reading English still pays in dong.
builder.Services.AddRequestCurrency(builder.Configuration);

builder.Services.AddGrpc();
builder.Services.AddGrpcHealthChecks();
builder.Services.AddGrpcReflection();

builder.Services.AddMassTransit(x =>
{
    // A consumer's class name becomes its queue name. Cart is the THIRD service with an
    // OrderCompletedConsumer (Inventory and Order have one each); without this prefix all three bind
    // one queue and compete for a single copy of the event. That exact collision once settled an order
    // while its stock stayed held, with every unit test green. See CLAUDE.md, Gotchas.
    x.SetEndpointNameFormatter(new DefaultEndpointNameFormatter(prefix: "CartSvc", includeNamespace: false));

    x.AddConsumer<OrderSubmittedConsumer>();
    x.AddConsumer<OrderCompletedConsumer>();
    x.AddConsumer<OrderFailedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
        var rabbitUser = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest";
        var rabbitPass = Environment.GetEnvironmentVariable("RABBITMQ_PASS") ?? "guest";

        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });

        // OrderId on every log line and span written while consuming a message about an order, so one
        // Seq query - OrderId = '...' - returns a checkout across every service (feature 013).
        cfg.UseConsumeFilter(typeof(OrderIdLogScopeFilter<>), context);

        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<CartDbContext>(name: "cart_postgres_db");

// Logs and traces over OTLP to Seq when OTLP_ENDPOINT is set; nothing otherwise (feature 013).
builder.AddObservability("cart");

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseRequestLanguage();
app.UseRequestCurrency();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Reachable only on the HTTP/2 endpoint. The container health check keeps probing REST /health:
// curl cannot speak gRPC health, and a probe that cannot fail is worse than none.
app.MapGrpcService<CartReadingService>();
app.MapGrpcHealthChecksService();
app.MapGrpcReflectionService();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            service = "Cart Service",
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.ToString()
            })
        });
    }
});

if (Environment.GetEnvironmentVariable("RUN_MIGRATIONS_ON_STARTUP") == "true")
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    await migrationScope.ServiceProvider
        .GetRequiredService<CartDbContext>()
        .Database.MigrateAsync();
}

// Both addresses are declared in ConfigureKestrel above; passing one here would be a third opinion
// about where to listen, and the one that loses would lose silently.
app.Run();
