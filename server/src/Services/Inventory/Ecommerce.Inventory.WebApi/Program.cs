using Ecommerce.Inventory.Application;
using Ecommerce.Inventory.Infrastructure;
using Ecommerce.Inventory.Infrastructure.BackgroundServices;
using Ecommerce.Inventory.Infrastructure.Persistence;
using Ecommerce.Inventory.WebApi.Consumers;
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

var reservationTtl = Environment.GetEnvironmentVariable("INVENTORY_RESERVATION_TTL_MINUTES");
if (!string.IsNullOrEmpty(reservationTtl))
{
    builder.Configuration["Inventory:ReservationTtlMinutes"] = reservationTtl;
}

var sweepInterval = Environment.GetEnvironmentVariable("INVENTORY_SWEEP_INTERVAL_SECONDS");
if (!string.IsNullOrEmpty(sweepInterval))
{
    builder.Configuration["Inventory:SweepIntervalSeconds"] = sweepInterval;
}

// Override Configurations from Environment Variables for Inventory Database (Port 5437)
var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "123456";
var dbName = Environment.GetEnvironmentVariable("INVENTORY_DB_NAME") ?? "ecommerce_inventory_db";
var dbPort = Environment.GetEnvironmentVariable("INVENTORY_DB_PORT") ?? "5437";

builder.Configuration["ConnectionStrings:DefaultConnection"] =
    $"Host=localhost;Database={dbName};Username={dbUser};Password={dbPassword};Port={dbPort}";

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.Configure<ReservationExpiryOptions>(
    builder.Configuration.GetSection(ReservationExpiryOptions.SectionName));

builder.Services.AddHostedService<ReservationExpirySweeper>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ReserveInventoryConsumer>();
    x.AddConsumer<ReleaseInventoryConsumer>();
    x.AddConsumer<OrderCompletedConsumer>();
    x.AddConsumer<ProductCreatedConsumer>();

    // Transport-level duplicate suppression, applied to every receive endpoint. The unique
    // (OrderId, ProductId) constraint is the actual guarantee; this keeps the common case from
    // having to reach it.
    x.AddConfigureEndpointsCallback((context, _, cfg) =>
        cfg.UseEntityFrameworkOutbox<InventoryDbContext>(context));

    x.AddEntityFrameworkOutbox<InventoryDbContext>(o =>
    {
        o.UsePostgres();

        // Outbox for the replies this service publishes; the inbox below deduplicates the
        // deliveries it receives. Both write to the same database as the stock rows, so a message
        // and the change it describes commit together or not at all.
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
    .AddDbContextCheck<InventoryDbContext>(name: "inventory_postgres_db");

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
            service = "Inventory Service",
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

app.Run("http://localhost:5060");
