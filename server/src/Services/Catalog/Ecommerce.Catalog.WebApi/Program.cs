using Ecommerce.Shared.Localization;
using Ecommerce.Shared.Money;
using Ecommerce.Shared.Observability;
using Ecommerce.Catalog.WebApi.Grpc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Ecommerce.Catalog.Application;
using Ecommerce.Catalog.Infrastructure;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Catalog.WebApi.Consumers;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Middlewares;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// TWO endpoints: HTTP/1.1 for REST, HTTP/2 for gRPC. Both are declared here, and
// that is not a style choice - see the warning below.
//
// One plaintext port cannot serve both protocols, because telling HTTP/1.1 and
// HTTP/2 apart needs ALPN and ALPN is part of the TLS handshake. Measured on
// .NET 10 against a control:
//
//     Http1AndHttp2   --http1.1               -> proto=1.1 code=200
//     Http1AndHttp2   --http2-prior-knowledge -> proto=0   code=000   (refused)
//     Http2           --http2-prior-knowledge -> proto=2   code=200   (works)
//
// No TLS, because this is the shape a service mesh produces: the application
// speaks cleartext HTTP/2 and a sidecar handles mTLS. IF THIS EVER LEAVES A
// TRUSTED NETWORK that stops being true.
//
// ⚠️ CALLING ListenAnyIP AT ALL REPLACES ASPNETCORE_URLS - it does not add to it.
// Configuring only the gRPC endpoint here silently unbound REST: the container
// came up listening on 8081 alone, answered nothing on 8080, and went unhealthy.
// Kestrel says so, in a warning nobody reads:
//
//     Overriding address(es) 'http://+:8080'. Binding to endpoints defined via
//     IConfiguration and/or UseKestrel() instead.
//
// So the HTTP port is declared here too, derived from ASPNETCORE_URLS exactly as
// the fallback at the end of this file derives it - localhost when unset so
// start-dev keeps its pinned port, all interfaces when a container sets it.
var listenUrlsAtStartup = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
var inContainer = !string.IsNullOrWhiteSpace(listenUrlsAtStartup);

var httpPort = PortFrom(listenUrlsAtStartup) ?? 5057;
var grpcPort = int.TryParse(Environment.GetEnvironmentVariable("CATALOG_GRPC_PORT"), out var parsedGrpc)
    ? parsedGrpc
    : 5157;

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

    // "http://+:8080" or "http://0.0.0.0:8080;http://..." - the first one wins.
    var first = urls.Split(';', StringSplitOptions.RemoveEmptyEntries)[0];
    var lastColon = first.LastIndexOf(':');

    return lastColon >= 0 && int.TryParse(first[(lastColon + 1)..].TrimEnd('/'), out var port)
        ? port
        : null;
}


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

// Override Configurations from Environment Variables for Catalog Database (Port 5433)
var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "123456";
var dbName = Environment.GetEnvironmentVariable("CATALOG_DB_NAME") ?? "ecommerce_catalog_db";
var dbPort = Environment.GetEnvironmentVariable("CATALOG_DB_PORT") ?? "5433";
var dbHost = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";

builder.Configuration["ConnectionStrings:DefaultConnection"] =
    $"Host={dbHost};Database={dbName};Username={dbUser};Password={dbPassword};Port={dbPort}";

// Add services to the container.
builder.Services.AddControllers().AddJsonOptions(options =>
{
    // Reject a field the model does not have, instead of ignoring it.
    //
    // CreateProduct used to take stockQuantity. With the default handling, a caller still sending it
    // would get a 200 and reasonably believe they had set stock - an input that appears to do
    // something and does not, which is the shape of the defect this feature exists to remove
    // (issue #4). Silently discarding it would replace a wrong number with a wrong impression.
    //
    // Applies to every Catalog endpoint, not only this one. That is a deliberate widening: there is
    // no client in this repository that sends unknown fields, and an unknown field is far more often
    // a typo than an intention.
    options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
});
builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);

// Which language a request wants to be answered in (specs/021). A service whose responses carry text
// a customer reads needs this; it refuses to start if the default is not one it supports.
builder.Services.AddRequestLanguage(builder.Configuration);

// Which currency the amounts in a response are in (specs/022). Separate from the language on
// purpose: a Vietnamese person reading English still pays in dong.
builder.Services.AddRequestCurrency(builder.Configuration);

builder.Services.AddMassTransit(x =>
{
    // Catalog's first consumer. Until now this service only published, which is why the product
    // listing carried a stock number nothing could update (issue #4).
    x.AddConsumer<StockAvailabilityChangedConsumer>();

    // Queue names are derived from consumer CLASS names, and two services naming a consumer the
    // same thing bind to one queue and COMPETE for it — each message reaches one of them instead of
    // both. Feature 003 shipped exactly that defect between Inventory and Order; it settled the
    // order and left the stock held, with all fifteen unit tests green. The prefix makes the
    // collision impossible rather than unlikely.
    x.SetEndpointNameFormatter(new DefaultEndpointNameFormatter(prefix: "CatalogSvc", includeNamespace: false));

    // Transport-level duplicate suppression on every receive endpoint. The time-compared UPDATE in
    // ProductRepository.TryRecordAvailabilityAsync is the actual guarantee — this only keeps the
    // ordinary redelivery from having to reach the database. No migration was needed: InboxState
    // has been in this database since 20260902161405_AddMassTransitOutbox.
    x.AddConfigureEndpointsCallback((context, _, cfg) =>
        cfg.UseEntityFrameworkOutbox<CatalogDbContext>(context));

    x.AddEntityFrameworkOutbox<CatalogDbContext>(o =>
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

builder.Services.AddGrpc();
builder.Services.AddGrpcHealthChecks();

// Reflection, so the service can be poked by hand with grpcurl. Without it a caller
// must already hold the .proto to ask anything, which makes diagnosing a live
// endpoint need a checkout of this repository.
//
// It does publish the service surface to anyone who can reach the port. For an
// internal endpoint on a trusted network that is the same trade already made by
// serving h2c without TLS - and it stops being acceptable in the same moment, if
// this port is ever exposed to anything untrusted.
builder.Services.AddGrpcReflection();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<CatalogDbContext>(name: "catalog_postgres_db");

// Logs and traces over OTLP to Seq when OTLP_ENDPOINT is set; nothing otherwise (feature 013).
builder.AddObservability("catalog");

var app = builder.Build();

// Resolved now, not on the first upload: an image root that cannot be written stops the service here,
// as the constitution asks of anything required, instead of answering 500 later (specs/019 D8).
app.Services.GetRequiredService<Ecommerce.Catalog.Application.Common.Interfaces.IProductImageStore>();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Says which language the response actually came back in, after the fallback.
app.UseRequestLanguage();
app.UseRequestCurrency();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Only reachable on the HTTP/2 endpoint above; the REST port cannot carry it.
app.MapGrpcService<CatalogPricingService>();

// gRPC health is its own protocol and curl cannot speak it, so the CONTAINER health check keeps
// probing REST /health and is deliberately not repointed here - a probe that cannot fail is worse
// than no probe. The consequence, stated rather than left to be found: Catalog can report healthy
// over REST while this endpoint is broken, and what catches that is the end-to-end check.
app.MapGrpcHealthChecksService();
app.MapGrpcReflectionService();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            service = "Catalog Service",
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
});


// Applying migrations from inside the service exists for one reason: a runtime image has neither
// the SDK nor the source, so `dotnet ef database update` - which is how start-dev.sh and CI create
// these schemas - cannot run there. Off unless asked, because "started successfully" and "was
// allowed to alter the schema" should not be the same event in a real deployment.
if (Environment.GetEnvironmentVariable("RUN_MIGRATIONS_ON_STARTUP") == "true")
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    await migrationScope.ServiceProvider
        .GetRequiredService<CatalogDbContext>()
        .Database.MigrateAsync();
}

// ASPNETCORE_URLS is still honoured - it is read at the top of this file, where the
// Kestrel endpoints are declared, because adding a second protocol made declaring
// them explicitly unavoidable. A container binds 0.0.0.0 and start-dev binds
// localhost on its pinned port, exactly as before.
//
// Passing an address here as well would be a third opinion about where to listen,
// and the one that loses would lose silently.
app.Run();

