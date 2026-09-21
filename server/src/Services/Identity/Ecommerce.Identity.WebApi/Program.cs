using Ecommerce.Shared.Observability;
using Microsoft.EntityFrameworkCore;
using Ecommerce.Application;
using Ecommerce.Infrastructure;
using Ecommerce.Infrastructure.Persistence;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Middlewares;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Ecommerce.WebApi.Grpc;

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

var envAdminEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL");
if (!string.IsNullOrEmpty(envAdminEmail))
{
    builder.Configuration["AdminUser:Email"] = envAdminEmail;
}

var envAdminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
if (!string.IsNullOrEmpty(envAdminPassword))
{
    builder.Configuration["AdminUser:Password"] = envAdminPassword;
}

var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "123456";
var dbName = Environment.GetEnvironmentVariable("IDENTITY_DB_NAME") ?? "ecommerce_identity_db";
var dbPort = Environment.GetEnvironmentVariable("IDENTITY_DB_PORT") ?? "5432";
var dbHost = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";

builder.Configuration["ConnectionStrings:DefaultConnection"] =
    $"Host={dbHost};Database={dbName};Username={dbUser};Password={dbPassword};Port={dbPort}";

// Two endpoints, declared together (feature 011). Identity now serves the address book to Order over
// gRPC, and one plaintext port cannot carry both HTTP/1.1 and HTTP/2 - telling them apart needs ALPN,
// which is part of TLS (measured in feature 009).
//
// CALLING ListenAnyIP AT ALL REPLACES ASPNETCORE_URLS - it does not add to it. Declaring only the gRPC
// endpoint silently unbinds REST; that is how Catalog went unhealthy the first time. So the HTTP port
// is declared here too, derived from ASPNETCORE_URLS - localhost:5056 when unset, so start-dev keeps
// its pinned port, all interfaces when a container sets it - and nothing else decides where to listen.
var listenUrlsAtStartup = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
var inContainer = !string.IsNullOrWhiteSpace(listenUrlsAtStartup);

var httpPort = PortFrom(listenUrlsAtStartup) ?? 5056;
var grpcPort = int.TryParse(Environment.GetEnvironmentVariable("IDENTITY_GRPC_PORT"), out var parsedGrpc)
    ? parsedGrpc
    : 5156;

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

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddControllers();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.AddGrpc();
builder.Services.AddGrpcHealthChecks();

// Reflection, so the endpoint can be poked by hand with grpcurl. It publishes the service surface to
// anyone who can reach the port - the same trade as serving h2c without TLS, acceptable only while
// that port is internal.
builder.Services.AddGrpcReflection();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>(name: "identity_postgres_db");

// Logs and traces over OTLP to Seq when OTLP_ENDPOINT is set; nothing otherwise (feature 013).
builder.AddObservability("identity");

var app = builder.Build();

// Seed roles and the bootstrap administrator before serving traffic.
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DataInitializer>();

    try
    {
        await initializer.SeedAsync();
    }
    catch (Exception exception)
    {
        app.Logger.LogError(
            exception,
            "Database seeding failed. Have the EF Core migrations been applied?");
        throw;
    }
}

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Only reachable on the HTTP/2 endpoint above.
app.MapGrpcService<AddressReadingService>();
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
            service = "Identity Service",
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
        .GetRequiredService<ApplicationDbContext>()
        .Database.MigrateAsync();
}

// Where to listen was decided once, in ConfigureKestrel above. No URL here: a second opinion about
// the address is how REST and gRPC end up disagreeing.
app.Run();

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
