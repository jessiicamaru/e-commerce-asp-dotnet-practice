using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Observability;
using Microsoft.EntityFrameworkCore;
using Ecommerce.Payment.Application;
using Ecommerce.Payment.Application.Common.Interfaces;
using Ecommerce.Payment.Infrastructure;
using Ecommerce.Payment.Infrastructure.Persistence;
using Ecommerce.Payment.WebApi.Consumers;
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

// The .env loader above runs after CreateBuilder, so the environment-variable configuration
// provider has already been built and cannot see anything set here. Map what is needed by hand —
// leaving JWT_SECRET out is the documented way to have every token silently rejected.
var envJwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
if (!string.IsNullOrEmpty(envJwtSecret))
{
    builder.Configuration["JwtSettings:Secret"] = envJwtSecret;
}

var paymentOutcome = Environment.GetEnvironmentVariable("PAYMENT_OUTCOME");
if (!string.IsNullOrEmpty(paymentOutcome))
{
    builder.Configuration["Payment:Outcome"] = paymentOutcome;
}

// Override Configurations from Environment Variables for Payment Database (Port 5438)
var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "123456";
var dbName = Environment.GetEnvironmentVariable("PAYMENT_DB_NAME") ?? "ecommerce_payment_db";
var dbPort = Environment.GetEnvironmentVariable("PAYMENT_DB_PORT") ?? "5438";
var dbHost = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";

builder.Configuration["ConnectionStrings:DefaultConnection"] =
    $"Host={dbHost};Database={dbName};Username={dbUser};Password={dbPassword};Port={dbPort}";

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);

// Who did what, through the outbox with every change (specs/041).
builder.Services.AddAuditTrail("payment");

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ProcessPaymentConsumer>();
    // specs/039: a cancelled order is refunded. Registered, or it never runs and never complains.
    x.AddConsumer<RefundCancelledOrderConsumer>();

    x.AddConfigureEndpointsCallback((context, _, cfg) =>
        cfg.UseEntityFrameworkOutbox<PaymentDbContext>(context));

    x.AddEntityFrameworkOutbox<PaymentDbContext>(o =>
    {
        o.UsePostgres();

        // The payment row and the reply describing it commit together or not at all.
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
    .AddDbContextCheck<PaymentDbContext>(name: "payment_postgres_db");

// Logs and traces over OTLP to Seq when OTLP_ENDPOINT is set; nothing otherwise (feature 013).
builder.AddObservability("payment");

// What an amount with no stated currency means (specs/022). Payment has no request to negotiate
// from: it records the currency the saga handed it, and falls back to this when that is empty -
// which is what an in-flight message from an Order built before this feature carries.
builder.Services.Configure<Ecommerce.Shared.Money.CurrencyOptions>(
    builder.Configuration.GetSection(Ecommerce.Shared.Money.CurrencyOptions.SectionName));

var app = builder.Build();

// One of three independent signals that this service is a stand-in. A stub mistaken for the real
// thing fulfils every order without anyone being charged, so saying it once in a code comment is
// not enough: it is also on every payment row and in every health response.
var gateway = app.Services.GetRequiredService<IPaymentGateway>();

app.Logger.LogWarning(
    "Payment service started with the {Provider} gateway: NO MONEY IS MOVED. "
    + "Configured outcome is {Outcome}.",
    gateway.ProviderName,
    gateway.ConfiguredOutcome);

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
            service = "Payment Service",

            // Reported so that a service deliberately set to reject is never mistaken for a broken
            // one, and a healthy stub is never mistaken for a gateway that is really charging.
            provider = $"{gateway.ProviderName} - no money is moved",
            configuredOutcome = gateway.ConfiguredOutcome.ToString(),

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
        .GetRequiredService<PaymentDbContext>()
        .Database.MigrateAsync();
}

// Honour ASPNETCORE_URLS when the environment sets it - a container must bind 0.0.0.0, not
// localhost, or nothing outside it can connect however the ports are published. Falling back to
// the pinned address rather than dropping the argument keeps start-dev.sh working: with no
// argument every service would default to the same port and collide.
var listenUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");

if (string.IsNullOrWhiteSpace(listenUrls))
{
    app.Run("http://localhost:5061");
}
else
{
    app.Run();
}

