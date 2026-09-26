using Ecommerce.Shared.Email;
using Ecommerce.Shared.Notifications;
using Ecommerce.Shared.Audit;
using Ecommerce.Order.Application;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Infrastructure.Persistence;
using Ecommerce.Order.Infrastructure.Persistence.Repositories;
using Ecommerce.Order.Infrastructure.Shipping;
using Ecommerce.Order.Infrastructure.Tax;
using Ecommerce.Order.WebApi.Consumers;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Localization;
using Ecommerce.Shared.Money;
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

    public FakeCheckoutDependencies Checkout { get; } = new();

    /// <summary>The marketplace's commission, 10% like the shop. Settable: SC-002 changes it mid-test.</summary>
    public TestCommissionRate Commission { get; } = new();

    private TestLanguage LanguageHolder { get; } = new();

    private TestCurrency CurrencyHolder { get; } = new();

    /// <summary>
    /// The currency checkout runs in. Dong by default, like the shop. Settable per test, for the same
    /// reason as the language: the provider is built once for the whole collection.
    /// </summary>
    public Currency Currency
    {
        get => CurrencyHolder.Current;
        set => CurrencyHolder.Current = value;
    }

    /// <summary>
    /// The language checkout runs in. Vietnamese by default, like the shop. Settable per test - the
    /// provider is built once for the whole collection, so a language captured at registration would
    /// be the first test's forever.
    /// </summary>
    public string Language
    {
        get => LanguageHolder.Current;
        set => LanguageHolder.Current = value;
    }

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
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                // A price per currency (specs/022). The two lists are deliberately NOT a
                // conversion of each other - 5 USD is not 5,000 VND at any rate - so a test that
                // asserted on a converted number would be asserting on a coincidence.
                ["Shipping:Options:0:Code"] = "standard",
                ["Shipping:Options:0:Name"] = "Standard delivery",
                ["Shipping:Options:0:Prices:VND"] = "30000",
                ["Shipping:Options:0:Prices:USD"] = "5.00",
                ["Shipping:Options:1:Code"] = "express",
                ["Shipping:Options:1:Name"] = "Express delivery",
                ["Shipping:Options:1:Prices:VND"] = "60000",
                ["Shipping:Options:1:Prices:USD"] = "15.00",
                // Offered in dong ONLY, on purpose: FR-008 needs something to refuse, and a refusal
                // proved by an option nobody uses is worth more than one that breaks other tests.
                ["Shipping:Options:2:Code"] = "overnight",
                ["Shipping:Options:2:Name"] = "Overnight delivery",
                ["Shipping:Options:2:Prices:VND"] = "120000",
                ["Money:DefaultCurrency"] = "VND",
                ["Money:Supported:0:Code"] = "VND",
                ["Money:Supported:0:Decimals"] = "0",
                ["Money:Supported:1:Code"] = "USD",
                ["Money:Supported:1:Decimals"] = "2",
                ["Tax:DefaultRate"] = "0.10",
                ["Tax:Rates:VN"] = "0.10",
                ["Tax:Rates:GB"] = "0.20",
                ["Tax:Rates:US"] = "0.00"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddApplication();

        // The retrying execution strategy production configures (specs/069 found why it matters here): with it, a
        // transaction opened by hand throws unless it runs inside CreateExecutionStrategy().ExecuteAsync - and a
        // fixture without it passed code that answered 500 in every container.
        services.AddDbContext<OrderDbContext>(options => options.UseNpgsql(_connectionString,
            npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null)));
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<Ecommerce.Order.Application.Insights.IOrderInsights, OrderInsights>();
        services.AddScoped<IPayoutRepository, PayoutRepository>();
        services.AddScoped<Ecommerce.Order.Application.Returns.IReturnRepository, ReturnRepository>();
        services.AddScoped<Ecommerce.Order.Application.Vouchers.IVoucherRepository, VoucherRepository>();
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new Ecommerce.Order.Application.Returns.ReturnOptions()));
        services.AddSingleton<ICommissionRate>(Commission);

        // The one substitution. It is what the query tests use to say "the caller is this shopper",
        // and it is also the limit of what they prove: that the owner filter is applied to whatever
        // identity is handed in — not that the identity handed in at runtime came from a valid
        // token. That end of the path is exercised by the auth smoke script, not from here.
        services.AddSingleton<ICurrentUser>(CurrentUser);

        // No HTTP request in these tests, so the language is handed in rather than negotiated
        // (specs/021). Tests that care set Language.
        services.AddSingleton<IRequestLanguage>(_ => LanguageHolder);

        // ...and the currency, for the same reason (specs/022). Tests that care set Currency.
        services.AddSingleton<IRequestCurrency>(_ => CurrencyHolder);
        services.Configure<CurrencyOptions>(configuration.GetSection(CurrencyOptions.SectionName));

        // Checkout's three synchronous reads, faked - they are other services, and what the checkout
        // tests are about is what Order does with the answers. The real gRPC path is exercised end to
        // end by verify-saga.sh. The delivery options are the REAL class, reading real configuration.
        services.AddSingleton(Checkout);
        services.AddSingleton<ICartReader>(Checkout);
        services.AddSingleton<ICatalogPrices>(Checkout);
        services.AddSingleton<IAddressReader>(Checkout);
        services.AddSingleton<IShippingOptions, ConfiguredShippingOptions>();
        services.AddSingleton<ITaxRates, ConfiguredTaxRates>();

        // The real consumers, so at least one test per event proves the wiring and not only the
        // handler. The repetition tests dispatch the command directly instead — the guard is what
        // they are about, and going through the broker would add timing to an assertion that has
        // nothing to do with timing.
        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<OrderCompletedConsumer>();
        services.AddAuditTrail("order");
        services.AddNotifier();
        services.AddEmailSender();
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

    /// <summary>Settable: an Order test that needs an administrator says so.</summary>
    public HashSet<string> Roles { get; } = [];

    public bool IsInRole(string role) => Roles.Contains(role);
}

/// <summary>
/// What Cart, Catalog and Identity would answer at checkout. Tests set these before sending.
/// </summary>
public class FakeCheckoutDependencies : ICartReader, ICatalogPrices, IAddressReader
{
    public List<CartItem> Cart { get; set; } = [];
    public Dictionary<Guid, CatalogPrice> Prices { get; } = [];

    /// <summary>What Identity returns; <c>null</c> means "not found / not yours / no default".</summary>
    public AddressCopy? Address { get; set; }

    public Guid? LastAddressIdAsked { get; private set; }

    public Task<IReadOnlyList<CartItem>> GetMyCartAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CartItem>>(Cart);

    /// <summary>What language checkout asked Catalog to answer in (specs/021).</summary>
    public string? LastLanguageAsked { get; private set; }

    /// <summary>What currency checkout asked Catalog to price in (specs/022).</summary>
    public string? LastCurrencyAsked { get; private set; }

    public Task<IReadOnlyList<CatalogPrice>> GetPricesAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default,
        string language = "",
        string currency = "")
    {
        LastLanguageAsked = language;
        LastCurrencyAsked = currency;
        return Task.FromResult<IReadOnlyList<CatalogPrice>>(productIds.Select(id => Prices[id]).ToList());
    }

    public Task<AddressCopy?> GetMyAddressAsync(Guid? addressId, CancellationToken cancellationToken = default)
    {
        LastAddressIdAsked = addressId;
        return Task.FromResult(Address);
    }
}

[CollectionDefinition(nameof(OrderTestCollection))]
public class OrderTestCollection : ICollectionFixture<OrderTestFixture>;

/// <summary>A settable <see cref="IRequestLanguage"/>: these tests have no request to negotiate from.</summary>
public class TestLanguage : IRequestLanguage
{
    public string Current { get; set; } = "vi";
}

/// <summary>Test helpers that move time for the return window (specs/066).</summary>
public static class ReturnWindow
{
    /// <summary>
    /// Moves every delivered parcel of <paramref name="orderId"/> to 8 days ago - past the 7-day return window, so
    /// its money is due (specs/066). A parcel delivered "now" is still returnable, and so still on the way.
    /// </summary>
    public static async Task PassAsync(OrderTestFixture fixture, Guid orderId)
    {
        await using var scope = fixture.NewScope();
        await scope.ServiceProvider.GetRequiredService<OrderDbContext>().OrderShipments
            .Where(s => s.OrderId == orderId && s.DeliveredAt != null)
            .ExecuteUpdateAsync(x => x.SetProperty(s => s.DeliveredAt, DateTime.UtcNow.AddDays(-8)));
    }
}

/// <summary>A settable <see cref="ICommissionRate"/> (specs/037).</summary>
public class TestCommissionRate : ICommissionRate
{
    public decimal Current { get; set; } = 0.10m;
}

/// <summary>A settable <see cref="IRequestCurrency"/>, for the same reason.</summary>
public class TestCurrency : IRequestCurrency
{
    public Currency Current { get; set; } = new("VND", 0);
}
