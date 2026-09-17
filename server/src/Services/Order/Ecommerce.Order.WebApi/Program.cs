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
            Environment.SetEnvironmentVariable(key, value);
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

        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<OrderDbContext>(name: "order_postgres_db");

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

app.Run("http://localhost:5059");
