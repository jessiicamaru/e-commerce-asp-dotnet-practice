using Ecommerce.Cart.Application;
using Ecommerce.Cart.Application.Common.Interfaces;
using Ecommerce.Cart.Infrastructure.Persistence;
using Ecommerce.Cart.Infrastructure.Persistence.Repositories;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Localization;
using Ecommerce.Shared.Money;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Ecommerce.Cart.Tests;

/// <summary>
/// The real Application stack against a real PostgreSQL on 5439.
/// </summary>
/// <remarks>
/// The database is not faked on purpose. What these tests exist for - a checkout's lines removed
/// once, whichever order the events arrive in - rests on a row lock and a guarded flag, and an
/// in-memory provider has neither. It would pass against code that removes the lines twice.
/// </remarks>
public class CartTestFixture : IAsyncLifetime
{
    private string _databaseName = null!;
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "123456";
        _databaseName = $"cart_tests_{Guid.NewGuid():N}";
        _connectionString =
            $"Host=localhost;Port=5439;Database={_databaseName};Username=postgres;"
            + $"Password={password};Maximum Pool Size=20;Timeout=30;Command Timeout=60";

        await using (var admin = new NpgsqlConnection(
            $"Host=localhost;Port=5439;Database=postgres;Username=postgres;Password={password}"))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        await using var scope = For(Guid.NewGuid()).CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CartDbContext>().Database.MigrateAsync();
    }

    /// <summary>A provider whose ICurrentUser is <paramref name="userId"/> - a signed-in customer.</summary>
    public ServiceProvider For(Guid userId)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddDbContext<CartDbContext>(o => o.UseNpgsql(_connectionString));
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<ICheckoutOutcomeRepository, CheckoutOutcomeRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICatalogProducts, NoCatalog>();

        // No HTTP request here, so the language and currency are the shop's defaults
        // (specs/021, specs/022).
        services.AddSingleton<IRequestLanguage>(new FixedLanguage("vi"));
        services.AddSingleton<IRequestCurrency>(
            new FixedCurrency(new Ecommerce.Shared.Money.Currency("VND", 0)));
        services.AddSingleton<ICurrentUser>(new FixedUser(userId));
        return services.BuildServiceProvider(validateScopes: true);
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "123456";
        await using var admin = new NpgsqlConnection(
            $"Host=localhost;Port=5439;Database=postgres;Username=postgres;Password={password}");
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }

    private sealed class FixedUser(Guid id) : ICurrentUser
    {
        public Guid? Id { get; } = id;
        public string? Email => null;
        public bool IsAuthenticated => true;

        // These tests are about the cart's own data; no handler in them asks about a role.
        public bool IsInRole(string role) => false;
    }

    /// <summary>These tests are about the cart's own data; Catalog is never consulted.</summary>
    private sealed class NoCatalog : ICatalogProducts
    {
        public Task<CatalogDescription> DescribeAsync(
            IReadOnlyCollection<Guid> productIds,
            CancellationToken ct = default,
            string language = "",
            string currency = "")
            => Task.FromResult(CatalogDescription.Unreachable());
    }
}

[CollectionDefinition(nameof(CartTestCollection))]
public class CartTestCollection : ICollectionFixture<CartTestFixture>;
