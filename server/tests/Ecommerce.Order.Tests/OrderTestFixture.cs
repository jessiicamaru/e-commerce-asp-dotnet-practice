using Ecommerce.Order.Application;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Infrastructure.Persistence;
using Ecommerce.Order.Infrastructure.Persistence.Repositories;
using Ecommerce.Order.WebApi.Consumers;
using Ecommerce.Shared.Authentication;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Ecommerce.Order.Tests;

/// <summary>
/// The real Application stack against a real PostgreSQL, with MassTransit's in-memory harness
/// standing in for the broker.
/// <para>
/// The database is not faked on purpose. The guarantee these tests exist for — that a redelivered
/// settlement changes nothing — lives in the WHERE clause of an UPDATE, which only a database
/// evaluates. An in-memory provider would pass against code that re-settles a finished order.
/// </para>
/// </summary>
public class OrderTestFixture : IAsyncLifetime
{
    private const string AdminConnectionString =
        "Host=localhost;Port=5434;Database=postgres;Username=postgres;Password={0}";

    private string _databaseName = null!;
    private string _connectionString = null!;

    public ServiceProvider Services { get; private set; } = null!;

    public TestCurrentUser CurrentUser { get; } = new();

    public ITestHarness Harness => Services.GetRequiredService<ITestHarness>();

    public async Task InitializeAsync()
    {
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "123456";

        _databaseName = $"order_tests_{Guid.NewGuid():N}";

        // Bounded from the start, as in the payment fixture. Feature 001's concurrency test opened
        // one connection per concurrent request, exceeded max_connections, and failed as a
        // connection error rather than on its assertion — while appearing to pass on an earlier run.
        _connectionString =
            $"Host=localhost;Port=5434;Database={_databaseName};Username=postgres;"
            + $"Password={password};Maximum Pool Size=20;Timeout=30;Command Timeout=60";

        await using (var admin = new NpgsqlConnection(string.Format(AdminConnectionString, password)))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        Services = BuildProvider();

        await using (var scope = Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            await context.Database.MigrateAsync();
        }

        await Harness.Start();
    }

    private ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString
            })
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddApplication();

        services.AddDbContext<OrderDbContext>(options => options.UseNpgsql(_connectionString));
        services.AddScoped<IOrderRepository, OrderRepository>();

        // The one substitution. It is what the query tests use to say "the caller is this shopper",
        // and it is also the limit of what they prove: that the owner filter is applied to whatever
        // identity is handed in — not that the identity handed in at runtime came from a valid
        // token. That end of the path is exercised by the auth smoke script, not from here.
        services.AddSingleton<ICurrentUser>(CurrentUser);

        // The real consumers, so at least one test per event proves the wiring and not only the
        // handler. The repetition tests dispatch the command directly instead — the guard is what
        // they are about, and going through the broker would add timing to an assertion that has
        // nothing to do with timing.
        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<OrderCompletedConsumer>();
            x.AddConsumer<OrderFailedConsumer>();
        });

        return services.BuildServiceProvider(true);
    }

    public async Task DisposeAsync()
    {
        await Services.DisposeAsync();

        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "123456";

        await using var admin = new NpgsqlConnection(string.Format(AdminConnectionString, password));
        await admin.OpenAsync();

        await using var drop = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }

    public AsyncServiceScope NewScope() => Services.CreateAsyncScope();
}

/// <summary>
/// A settable <see cref="ICurrentUser"/>. Tests assign <see cref="Id"/> to choose whose request
/// they are making.
/// </summary>
public class TestCurrentUser : ICurrentUser
{
    public Guid? Id { get; set; }

    public string? Email { get; set; }

    public bool IsAuthenticated => Id is not null;
}

[CollectionDefinition(nameof(OrderTestCollection))]
public class OrderTestCollection : ICollectionFixture<OrderTestFixture>;
