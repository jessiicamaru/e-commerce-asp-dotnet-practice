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

        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<CatalogDbContext>(name: "catalog_postgres_db");

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

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

// Honour ASPNETCORE_URLS when the environment sets it - a container must bind 0.0.0.0, not
// localhost, or nothing outside it can connect however the ports are published. Falling back to
// the pinned address rather than dropping the argument keeps start-dev.sh working: with no
// argument every service would default to the same port and collide.
var listenUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");

if (string.IsNullOrWhiteSpace(listenUrls))
{
    app.Run("http://localhost:5057");
}
else
{
    app.Run();
}

