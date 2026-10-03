using Ecommerce.Shared.Messaging;
using Ecommerce.Shared.Email;
using Ecommerce.Shared.Notifications;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Insights;
using Ecommerce.Shared.Localization;
using Ecommerce.Shared.Money;
using Ecommerce.Shared.Observability;
using Microsoft.EntityFrameworkCore;
using Ecommerce.Order.Application;
using Ecommerce.Order.Infrastructure;
using Ecommerce.Order.Infrastructure.Persistence;
using Ecommerce.Order.WebApi.Consumers;
using Ecommerce.Order.WebApi.Grpc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Middlewares;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

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

            // Fall back, never override. A variable already set in the real environment wins.
            // That precedence is what makes an image configurable at all: a settings file that
            // reached a layer must not be able to ignore what the container is told at run time.
            // Before feature 005 this call was unconditional, which is why
            // `PAYMENT_OUTCOME=Reject dotnet run ...` was silently ignored.
            if (Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}

// Override Configurations from Environment Variables
var envJwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
if (!string.IsNullOrEmpty(envJwtSecret))
{
    builder.Configuration["JwtSettings:Secret"] = envJwtSecret;
}

// Override Configurations from Environment Variables for Order Database (Port 5434)
var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "123456";
var dbName = Environment.GetEnvironmentVariable("ORDER_DB_NAME") ?? "ecommerce_order_db";
var dbPort = Environment.GetEnvironmentVariable("ORDER_DB_PORT") ?? "5434";
var dbHost = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";

builder.Configuration["ConnectionStrings:DefaultConnection"] =
    $"Host={dbHost};Database={dbName};Username={dbUser};Password={dbPassword};Port={dbPort}";

// TWO endpoints since specs/112, when Order began serving gRPC (AccountStanding, asked by Identity): HTTP/1.1 for REST,
// HTTP/2 for gRPC, declared TOGETHER. Calling ListenAnyIP at all REPLACES ASPNETCORE_URLS rather than adding to it
// (feature 009, on Catalog), so the HTTP port is declared here too - derived from ASPNETCORE_URLS in a container,
// the pinned 5059 under start-dev - and the app.Run(url) fallback is gone: it would be a third opinion about where to
// listen. One plaintext port cannot serve both protocols (telling them apart needs ALPN, part of TLS).
var listenUrlsAtStartup = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
var inContainer = !string.IsNullOrWhiteSpace(listenUrlsAtStartup);

var httpPort = PortFrom(listenUrlsAtStartup) ?? 5059;
var grpcPort = int.TryParse(Environment.GetEnvironmentVariable("ORDER_GRPC_PORT"), out var parsedGrpc)
    ? parsedGrpc
    : 5159;

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

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddGrpc();
builder.Services.AddGrpcHealthChecks();
builder.Services.AddGrpcReflection();
builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);

// Who did what, through the outbox with every change (specs/041).
builder.Services.AddAuditTrail("order");

// The shop's days for every insight (specs/082): Insights:TimeZone, Asia/Ho_Chi_Minh by default.
builder.Services.AddInsightsCalendar(builder.Configuration);
builder.Services.AddNotifier();
// Order confirmations (specs/060): requested through the outbox, sent by Identity.
builder.Services.AddEmailSender();

// Which language a request wants to be answered in (specs/021). A service whose responses carry text
// a customer reads needs this; it refuses to start if the default is not one it supports.
builder.Services.AddRequestLanguage(builder.Configuration);

// Which currency the amounts in a response are in (specs/022). Separate from the language on
// purpose: a Vietnamese person reading English still pays in dong.
builder.Services.AddRequestCurrency(builder.Configuration);

builder.Services.AddMassTransit(x =>
{
    // Access tokens revoked by Identity - a lock, a ban, a password or a role changed - refused here within
    // seconds, on every instance (specs/065).
    x.AddAccessTokenRevocations("order");

    // The first consumers this service has ever had. Until now it published OrderSubmittedEvent and
    // then stopped taking part, which is why every order row read Submitted however checkout ended.
    x.AddConsumer<OrderCompletedConsumer>();
    x.AddConsumer<OrderFailedConsumer>();
    x.AddConsumer<EraseAccountFromOrdersConsumer>();

    // Queue names are derived from consumer CLASS names, and Inventory already has a class called
    // OrderCompletedConsumer. Without this prefix both services bind to a queue named
    // "OrderCompleted" and *compete* for it: each completion goes to one service or the other, so
    // roughly half of all orders would settle without Inventory ever confirming the stock, and the
    // other half would confirm the stock without the order ever settling.
    //
    // This was not theoretical — it happened. The first end-to-end run of this feature settled the
    // order and left its units held, and `rabbitmqctl list_queues` showed OrderCompleted with two
    // consumers. Publish/subscribe fans out per *endpoint*, not per service, and two services
    // naming a consumer the same thing collapses into one endpoint.
    x.SetEndpointNameFormatter(new DefaultEndpointNameFormatter(prefix: "OrderSvc", includeNamespace: false));

    // Transport-level duplicate suppression on every receive endpoint. The guarded UPDATE in
    // OrderRepository.TrySettleAsync is the actual guarantee — this only keeps the ordinary
    // redelivery from having to reach the database. No migration was needed: InboxState and
    // OutboxState have been in this database since 20260903142425_InitialOrderSchema, because
    // OrderDbContext has always called AddTransactionalOutboxEntities().
    // A transient database failure (a serialization failure under load) is retried in a new transaction - first,
    // so it wraps the outbox (specs/145, #299).
    x.AddConfigureEndpointsCallback((context, _, cfg) =>
    {
        cfg.UseTransientRetry();
        cfg.UseEntityFrameworkOutbox<OrderDbContext>(context);
    });

    x.AddEntityFrameworkOutbox<OrderDbContext>(o =>
    {
        o.UsePostgres();
        o.UseBusOutbox();
    });

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
    .AddDbContextCheck<OrderDbContext>(name: "order_postgres_db");

// Logs and traces over OTLP to Seq when OTLP_ENDPOINT is set; nothing otherwise (feature 013).
builder.AddObservability("order");

var app = builder.Build();

// Resolved now, not at the first checkout: a missing or malformed shipping, tax or commission setting stops
// the service here, where the log says why, instead of turning every checkout into a 500 (constitution:
// Configuration). One list, tested over the real registration (specs/103).
Ecommerce.Order.Infrastructure.RequiredSettings.Check(app.Services);

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();

// Says which language the response actually came back in, after the fallback.
app.UseRequestLanguage();
app.UseRequestCurrency();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Reachable only on the HTTP/2 endpoint. The container health check keeps probing REST /health.
app.MapGrpcService<AccountStandingService>();
app.MapGrpcHealthChecksService().AllowAnonymous();
app.MapGrpcReflectionService().AllowAnonymous();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            service = "Order Service",
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.ToString()
            })
        };
        await context.Response.WriteAsJsonAsync(response);
    }
}).AllowAnonymous(); // A probe carries no token; the fallback policy would refuse it (specs/089).


// Applying migrations from inside the service exists for one reason: a runtime image has neither
// the SDK nor the source, so `dotnet ef database update` - which is how start-dev.sh and CI create
// these schemas - cannot run there. Off unless asked, because "started successfully" and "was
// allowed to alter the schema" should not be the same event in a real deployment.
if (Environment.GetEnvironmentVariable("RUN_MIGRATIONS_ON_STARTUP") == "true")
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    await migrationScope.ServiceProvider
        .GetRequiredService<OrderDbContext>()
        .Database.MigrateAsync();
}

// Configured delivery options and the carrier that the table does not have yet (specs/098) - never overwriting what
// an administrator changed. After the migrations above, which may be what created the table.
await using (var seedScope = app.Services.CreateAsyncScope())
{
    await Ecommerce.Order.Infrastructure.Shipping.DeliverySeed.RunAsync(
        seedScope.ServiceProvider.GetRequiredService<OrderDbContext>(),
        seedScope.ServiceProvider.GetRequiredService<Ecommerce.Order.Infrastructure.Shipping.ConfiguredShippingOptions>(),
        app.Configuration);
}

// Where to listen is decided once, by the Kestrel endpoints above.
app.Run();

