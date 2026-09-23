using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Authentication;
using Ecommerce.Inventory.Application.Common;
using System.Security.Claims;
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

        // Who the tests are acting as, and what Catalog would answer (specs/031). Both settable,
        // because the refusals under test are exactly "this caller, that owner".
        services.AddSingleton(Caller);
        services.AddSingleton<ICurrentUser>(Caller);
        services.AddSingleton(Owners);
        services.AddSingleton<IProductOwnership>(Owners);

        services.AddMassTransitTestHarness();

        services.AddAuditTrail("inventory");

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

    /// <summary>Who the tests act as. An administrator unless a test says otherwise.</summary>
    public TestCaller Caller { get; } = new();

    /// <summary>What Catalog would say about who owns a variant.</summary>
    public TestOwners Owners { get; } = new();

    /// <summary>A fresh DI scope, mirroring one message delivery or one HTTP request.</summary>
    public AsyncServiceScope NewScope() => Services.CreateAsyncScope();
}

[CollectionDefinition(nameof(InventoryTestCollection))]
public class InventoryTestCollection : ICollectionFixture<InventoryTestFixture>;

/// <summary>
/// Who a test is acting as. An administrator by default, which is what every stock write required
/// before sellers could set their own (specs/031).
/// </summary>
/// <remarks>
/// ⚠️ A test that changes this must put it back. The same shared-singleton trap that made eleven
/// Catalog image tests fail when SellerOwnershipTests left the caller as a seller.
/// </remarks>
public class TestCaller : ICurrentUser
{
    public Guid? Id { get; set; }

    public string? Email { get; set; }

    public bool IsAuthenticated { get; set; } = true;

    public List<string> Roles { get; set; } = [RoleNames.Admin];

    public ClaimsPrincipal? Principal => null;

    public bool IsInRole(string role) => Roles.Contains(role);

    /// <summary>Act as an administrator again. Call it in a Dispose, not by remembering to.</summary>
    public void BeAdmin()
    {
        Id = null;
        Roles = [RoleNames.Admin];
    }

    public void BeSeller(Guid sellerId)
    {
        Id = sellerId;
        Roles = [RoleNames.Seller];
    }
}

/// <summary>
/// What Catalog would answer. A variant nobody registered here is ABSENT, which is how a variant
/// that does not exist reaches the code under test.
/// </summary>
public class TestOwners : IProductOwnership
{
    private readonly Dictionary<Guid, VariantOwnership> _owners = [];

    /// <summary>Set when a test wants the lookup itself to fail, rather than to answer.</summary>
    public Exception? Throws { get; set; }

    public int Calls { get; private set; }

    public void OwnedBy(Guid variantId, Guid? sellerId) =>
        _owners[variantId] = new VariantOwnership(variantId, Guid.CreateVersion7(), sellerId);

    public void Clear()
    {
        _owners.Clear();
        Throws = null;
        Calls = 0;
    }

    public Task<VariantOwnership?> GetAsync(Guid variantId, CancellationToken cancellationToken = default)
    {
        Calls++;
        if (Throws is { } error)
        {
            throw error;
        }
        return Task.FromResult(_owners.GetValueOrDefault(variantId));
    }
}
