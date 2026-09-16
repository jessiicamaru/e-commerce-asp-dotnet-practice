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
            Environment.SetEnvironmentVariable(key, value);
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

builder.Configuration["ConnectionStrings:DefaultConnection"] =
    $"Host=localhost;Database={dbName};Username={dbUser};Password={dbPassword};Port={dbPort}";

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ProcessPaymentConsumer>();

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

        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<PaymentDbContext>(name: "payment_postgres_db");

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

app.Run("http://localhost:5061");
