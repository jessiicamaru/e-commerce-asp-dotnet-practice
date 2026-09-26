using Ecommerce.Shared.Notifications;
using Ecommerce.Shared.Audit;
using Ecommerce.Catalog.Application;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Infrastructure.Images;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Localization;
using Ecommerce.Shared.Money;
using Ecommerce.Catalog.Infrastructure.Persistence.Repositories;
using Ecommerce.Catalog.WebApi.Consumers;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// The real Application stack against a real PostgreSQL, with MassTransit's in-memory harness
/// standing in for the broker.
/// <para>
/// The database is not faked on purpose. The guarantee these tests exist for — that a redelivered
/// <i>or overtaken</i> announcement changes nothing — lives in the WHERE clause of an UPDATE, which
/// only a database evaluates. An in-memory provider would pass against code that lets an older
/// observation win.
/// </para>
/// </summary>
public class CatalogTestFixture : IAsyncLifetime
{
    private const string AdminConnectionString =
        "Host=localhost;Port=5433;Database=postgres;Username=postgres;Password={0}";

    private string _databaseName = null!;
    private string _connectionString = null!;

    public ServiceProvider Services { get; private set; } = null!;

    public ITestHarness Harness => Services.GetRequiredService<ITestHarness>();

    /// <summary>The real file store, in a temporary directory, with switches to make it fail.</summary>
    public TestImageStore Images { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "123456";

        _databaseName = $"catalog_tests_{Guid.NewGuid():N}";

        // Bounded from the start. Feature 001's concurrency test opened one connection per
        // concurrent request, exceeded max_connections, and failed as a connection error rather
        // than on its assertion — while appearing to pass on an earlier run.
        _connectionString =
            $"Host=localhost;Port=5433;Database={_databaseName};Username=postgres;"
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
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
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

        services.AddDbContext<CatalogDbContext>(options => options.UseNpgsql(_connectionString));
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IProductViewRepository, ProductViewRepository>();
        services.AddScoped<ISavedProductRepository, SavedProductRepository>();
        services.AddScoped<IProductQuestionRepository, ProductQuestionRepository>();

        // The orphan scan (specs/033) needs one read, not twenty-one, so it depends on the
        // narrow interface the repository also implements. Forwarded rather than registered
        // separately, so both resolve to the SAME instance inside a scope.
        services.AddScoped<ILiveImageKeys>(sp => sp.GetRequiredService<IProductRepository>());
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ISellerRepository, SellerRepository>();

        // Who is writing (specs/027). An ADMINISTRATOR by default, because that is who every one
        // of these tests was before sellers existed and it keeps them testing what they are about.
        // A test that cares about ownership sets Caller to a seller and says so.
        services.AddSingleton<TestCaller>();
        services.AddSingleton<ICurrentUser>(sp => sp.GetRequiredService<TestCaller>());

        // No request to negotiate from: a test says which language it is asking in.
        services.AddSingleton<TestLanguage>();
        services.AddSingleton<IRequestLanguage>(sp => sp.GetRequiredService<TestLanguage>());

        // Stated rather than left to a default: neither options type has one any more, because the
        // configuration binder APPENDS to a non-empty array instead of replacing it - found while
        // adding currencies (specs/022). The translation and price validators read these lists.
        services.AddSingleton<Microsoft.Extensions.Options.IOptions<LanguageOptions>>(
            Microsoft.Extensions.Options.Options.Create(new LanguageOptions
            {
                DefaultLanguage = "vi",
                Supported = ["vi", "en"],
            }));

        services.AddSingleton<Microsoft.Extensions.Options.IOptions<CurrencyOptions>>(
            Microsoft.Extensions.Options.Options.Create(new CurrencyOptions
            {
                DefaultCurrency = "VND",
                Supported =
                [
                    new() { Code = "VND", Decimals = 0 },
                    new() { Code = "USD", Decimals = 2 },
                ],
            }));

        // ...and a test says which currency it is asking in.
        services.AddSingleton<TestCurrency>();
        services.AddSingleton<IRequestCurrency>(sp => sp.GetRequiredService<TestCurrency>());

        // Product images (specs/019): the REAL filesystem store, in a directory of its own.
        Images = new TestImageStore(new FileSystemProductImageStore(
            Path.Combine(Path.GetTempPath(), $"catalog_images_{Guid.NewGuid():N}")));
        services.AddSingleton<IProductImageStore>(Images);

        // The real consumer, so at least one test proves the wiring and not only the handler.
        services.AddMassTransitTestHarness(x => x.AddConsumer<StockAvailabilityChangedConsumer>());
        services.AddAuditTrail("catalog");
        services.AddNotifier();

        return services.BuildServiceProvider(true);
    }

    public async Task DisposeAsync()
    {
        await Services.DisposeAsync();

        if (Directory.Exists(Images.Inner.Root))
        {
            Directory.Delete(Images.Inner.Root, recursive: true);
        }

        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "123456";

        await using var admin = new NpgsqlConnection(string.Format(AdminConnectionString, password));
        await admin.OpenAsync();

        await using var drop = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// A scope, optionally answering in a given language (specs/021). There is no HTTP request here,
    /// so the language is handed in rather than negotiated.
    /// </summary>
    public AsyncServiceScope NewScope(string? language = null, Currency? currency = null)
    {
        var scope = Services.CreateAsyncScope();

        if (language is not null)
        {
            scope.ServiceProvider.GetRequiredService<TestLanguage>().Current = language;
        }

        if (currency is not null)
        {
            scope.ServiceProvider.GetRequiredService<TestCurrency>().Current = currency;
        }

        return scope;
    }
}

[CollectionDefinition(nameof(CatalogTestCollection))]
public class CatalogTestCollection : ICollectionFixture<CatalogTestFixture>;

/// <summary>
/// The real store, plus the failures a test needs to be able to cause: a write that fails, and a
/// delete that fails.
/// </summary>
public sealed class TestImageStore(FileSystemProductImageStore inner) : IProductImageStore
{
    public FileSystemProductImageStore Inner { get; } = inner;

    public bool FailWrites { get; set; }

    public bool FailDeletes { get; set; }

    /// <summary>Runs after the bytes are written and before the handler switches the row - where a concurrent writer would land.</summary>
    public Func<Task>? AfterSave { get; set; }

    public IReadOnlyList<string> Files() =>
        Directory.GetFiles(Inner.Root).Select(Path.GetFileName).Where(f => !f!.StartsWith('.')).ToList()!;

    public async Task SaveAsync(string key, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        if (FailWrites)
        {
            throw new IOException("Simulated storage failure.");
        }

        await Inner.SaveAsync(key, content, cancellationToken);

        if (AfterSave is { } hook)
        {
            AfterSave = null;
            await hook();
        }
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default) =>
        Inner.OpenReadAsync(key, cancellationToken);

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) =>
        FailDeletes ? throw new IOException("Simulated storage failure.") : Inner.DeleteAsync(key, cancellationToken);

    public IAsyncEnumerable<StoredImage> ListAsync(CancellationToken cancellationToken = default) =>
        Inner.ListAsync(cancellationToken);
}

/// <summary>A settable <see cref="IRequestLanguage"/>: the tests' way of saying "asked in Vietnamese".</summary>
public class TestLanguage : IRequestLanguage
{
    public string Current { get; set; } = "vi";
}

/// <summary>
/// Who the tests are acting as (specs/027). An administrator unless a test says otherwise, which is
/// what every Catalog write required before sellers existed.
/// </summary>
public class TestCaller : ICurrentUser
{
    public Guid? Id { get; set; } = Guid.CreateVersion7();

    public string? Email { get; set; }

    /// <summary>What a review is signed with (specs/046).</summary>
    public string? GivenName { get; set; }

    public bool IsAuthenticated => true;

    /// <summary>Settable: a test about ownership becomes a seller by clearing Admin.</summary>
    public HashSet<string> Roles { get; } = ["Admin"];

    public bool IsInRole(string role) => Roles.Contains(role);
}

/// <summary>A settable <see cref="IRequestCurrency"/>: the tests' way of saying "asked in dong".</summary>
public class TestCurrency : IRequestCurrency
{
    public Currency Current { get; set; } = new("VND", 0);
}
