using Ecommerce.Shared.Localization;
using Ecommerce.Shared.Money;
using Ecommerce.Shared.Observability;
using Microsoft.EntityFrameworkCore;
using Ecommerce.Order.Application;
using Ecommerce.Order.Infrastructure;
using Ecommerce.Order.Infrastructure.Persistence;
using Ecommerce.Order.WebApi.Consumers;
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

// Add services to the container.
builder.Services.AddControllers();
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
    // The first consumers this service has ever had. Until now it published OrderSubmittedEvent and
    // then stopped taking part, which is why every order row read Submitted however checkout ended.
    x.AddConsumer<OrderCompletedConsumer>();
    x.AddConsumer<OrderFailedConsumer>();

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
    x.AddConfigureEndpointsCallback((context, _, cfg) =>
        cfg.UseEntityFrameworkOutbox<OrderDbContext>(context));

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

// Resolved now, not at the first checkout: a missing or malformed Shipping:Options stops the service
// here, where the log says why, instead of turning every checkout into a 500 (constitution:
// Configuration). ConfiguredShippingOptions validates in its constructor.
_ = app.Services.GetRequiredService<Ecommerce.Order.Application.Common.Interfaces.IShippingOptions>();
_ = app.Services.GetRequiredService<Ecommerce.Order.Application.Common.Interfaces.ITaxRates>();

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
});


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

// Honour ASPNETCORE_URLS when the environment sets it - a container must bind 0.0.0.0, not
// localhost, or nothing outside it can connect however the ports are published. Falling back to
// the pinned address rather than dropping the argument keeps start-dev.sh working: with no
// argument every service would default to the same port and collide.
var listenUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");

if (string.IsNullOrWhiteSpace(listenUrls))
{
    app.Run("http://localhost:5059");
}
else
{
    app.Run();
}

