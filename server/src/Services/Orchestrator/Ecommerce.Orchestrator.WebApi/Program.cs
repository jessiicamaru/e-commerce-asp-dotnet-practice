using Ecommerce.Shared.Observability;
using Ecommerce.Orchestrator.WebApi.StateMachines;
using Ecommerce.Orchestrator.WebApi.Timeouts;
using Ecommerce.Shared.Middlewares;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
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

// Override Configurations from Environment Variables for Orchestrator Database (Port 5436)
var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "123456";
var dbName = Environment.GetEnvironmentVariable("SAGA_DB_NAME") ?? "ecommerce_saga_db";
var dbPort = Environment.GetEnvironmentVariable("ORCHESTRATOR_DB_PORT") ?? "5436";
var dbHost = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";

var connectionString = $"Host={dbHost};Database={dbName};Username={dbUser};Password={dbPassword};Port={dbPort}";
builder.Configuration["ConnectionStrings:DefaultConnection"] = connectionString;

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Register Saga DbContext
builder.Services.AddDbContext<OrchestratorDbContext>(options =>
    options.UseNpgsql(
        connectionString,
        // See the note in every other service's AddInfrastructure: a container stack starts in
        // parallel, so reaching the database a moment early has to recover rather than fail.
        npgsql => npgsql.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null)));

// Register MassTransit with OrderStateMachine Saga
// How long an order waits for Payment once its stock is reserved (specs/053, #123). Read and checked now,
// so a timeout that is not shorter than Inventory's hold stops the service here rather than taking money
// for stock that went back on the shelf.
builder.Services.AddSingleton(PaymentTimeoutOptions.From(Environment.GetEnvironmentVariable));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<PaymentTimeoutSweeper>();

builder.Services.AddMassTransit(x =>
{
    x.AddSagaStateMachine<OrderStateMachine, OrderStateData>()
        .EntityFrameworkRepository(r =>
        {
            r.ConcurrencyMode = ConcurrencyMode.Optimistic;
            r.ExistingDbContext<OrchestratorDbContext>();
        });

    // The saga's messages and the saga's own state commit together, or not at all.
    //
    // Without this, `.Publish(...)` inside a state machine activity reaches the broker DURING the
    // consume - before the instance that caused it has been committed. The other services reply in
    // milliseconds, so the reply can arrive while the instance still does not exist, find nothing
    // to correlate to, and be discarded with no fault, no error queue and no log line.
    //
    // That is exactly what happened. Measured on a cold start, with MassTransit at Debug:
    //
    //     RECEIVE ... InventoryReservedEvent  (00:00:00.2936838)
    //     RECEIVE ... OrderSubmittedEvent     (00:00:05.4012902)
    //     SAGA:...:<id> Created  OrderSubmittedEvent
    //     SAGA:...:<id> Added    OrderSubmittedEvent      <- and nothing for InventoryReserved
    //
    // The first message this process handles pays for JIT, the EF model build and the first
    // database connection, so OrderSubmitted took 5.4s to commit while Inventory answered in 0.3s.
    // The order stayed 'Submitted' forever and its stock stayed held. Warm, the window never opens,
    // which is why every hand-check has passed and four sagas were stranded since 2026-09-03.
    //
    // Constitution III is not a style preference: an entity change and the events it causes commit
    // as one unit. Catalog, Inventory, Order and Payment have always done this; the orchestrator,
    // which causes more events than any of them, did not. See issue #15.
    x.AddConfigureEndpointsCallback((context, _, cfg) =>
        cfg.UseEntityFrameworkOutbox<OrchestratorDbContext>(context));

    x.AddEntityFrameworkOutbox<OrchestratorDbContext>(o =>
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

// /health, like every other service (specs/071, #115): the saga database, plus the broker - MassTransit adds its
// own "masstransit-bus" check to these. Until this the Orchestrator was the one service nothing could probe, and
// an order stuck in Submitted usually means it.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<OrchestratorDbContext>(name: "orchestrator_postgres_db");

// Logs and traces over OTLP to Seq when OTLP_ENDPOINT is set; nothing otherwise (feature 013).
builder.AddObservability("orchestrator");

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

app.MapGet("/", () => Results.Ok(new { Service = "Ecommerce.Orchestrator", Status = "Running", Environment = app.Environment.EnvironmentName }));

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            service = "Orchestrator",
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
        .GetRequiredService<OrchestratorDbContext>()
        .Database.MigrateAsync();
}

// Honour ASPNETCORE_URLS when the environment sets it - a container must bind 0.0.0.0, not
// localhost, or nothing outside it can connect however the ports are published. Falling back to
// the pinned address rather than dropping the argument keeps start-dev.sh working: with no
// argument every service would default to the same port and collide.
var listenUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");

if (string.IsNullOrWhiteSpace(listenUrls))
{
    app.Run("http://localhost:5058");
}
else
{
    app.Run();
}

