using Ecommerce.Inventory.Application;
using Ecommerce.Inventory.Application.Common.Interfaces;
using Ecommerce.Inventory.Infrastructure.Persistence;
using Ecommerce.Inventory.Infrastructure.Persistence.Repositories;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Ecommerce.Inventory.Tests;

/// <summary>
/// Spins up the real Application stack against a real PostgreSQL, with MassTransit's in-memory test
/// harness standing in for the broker.
/// <para>
/// The database is deliberately not faked. The guarantees under test — no overselling under
/// concurrency, a unique constraint stopping a duplicate reservation, row locks serialising two
/// racing orders — are the database's behaviour. An in-memory provider implements none of them, so
/// these tests would pass against code that oversells.
/// </para>
/// </summary>
public class InventoryTestFixture : IAsyncLifetime
{
    private const string AdminConnectionString =
        "Host=localhost;Port=5437;Database=postgres;Username=postgres;Password={0}";

    private string _databaseName = null!;
    private string _connectionString = null!;

    public ServiceProvider Services { get; private set; } = null!;

    public ITestHarness Harness => Services.GetRequiredService<ITestHarness>();

    public async Task InitializeAsync()
    {
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "123456";

        // One throwaway database per test class, so classes cannot see each other's rows.
        _databaseName = $"inventory_tests_{Guid.NewGuid():N}";
        // Pool size is bounded deliberately. The concurrency test fires 100 orders at once; one
        // connection each would exceed the server's max_connections and fail as a connection error
        // rather than as the overselling assertion the test exists to make. Npgsql queues the
        // surplus, so the orders still race — against a bounded number of connections.
        _connectionString =
            $"Host=localhost;Port=5437;Database={_databaseName};Username=postgres;"
            + $"Password={password};Maximum Pool Size=20;Timeout=30;Command Timeout=60";

        await using (var admin = new NpgsqlConnection(string.Format(AdminConnectionString, password)))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["Inventory:ReservationTtlMinutes"] = "15"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddApplication();

        services.AddDbContext<InventoryDbContext>(options => options.UseNpgsql(_connectionString));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IReservationRepository, ReservationRepository>();

        services.AddMassTransitTestHarness();

        Services = services.BuildServiceProvider(true);

        await using (var scope = Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            await context.Database.MigrateAsync();
        }

        await Harness.Start();
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

    /// <summary>A fresh DI scope, mirroring one message delivery or one HTTP request.</summary>
    public AsyncServiceScope NewScope() => Services.CreateAsyncScope();
}

[CollectionDefinition(nameof(InventoryTestCollection))]
public class InventoryTestCollection : ICollectionFixture<InventoryTestFixture>;
