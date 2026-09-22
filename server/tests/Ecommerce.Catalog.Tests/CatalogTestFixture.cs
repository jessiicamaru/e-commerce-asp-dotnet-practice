using Ecommerce.Catalog.Application;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Infrastructure.Images;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Shared.Localization;
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
        services.AddScoped<ICategoryRepository, CategoryRepository>();

        // No request to negotiate from: a test says which language it is asking in.
        services.AddSingleton<TestLanguage>();
        services.AddSingleton<IRequestLanguage>(sp => sp.GetRequiredService<TestLanguage>());
        services.Configure<LanguageOptions>(o => { });

        // Product images (specs/019): the REAL filesystem store, in a directory of its own.
        Images = new TestImageStore(new FileSystemProductImageStore(
            Path.Combine(Path.GetTempPath(), $"catalog_images_{Guid.NewGuid():N}")));
        services.AddSingleton<IProductImageStore>(Images);

        // The real consumer, so at least one test proves the wiring and not only the handler.
        services.AddMassTransitTestHarness(x => x.AddConsumer<StockAvailabilityChangedConsumer>());

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
    public AsyncServiceScope NewScope(string? language = null)
    {
        var scope = Services.CreateAsyncScope();

        if (language is not null)
        {
            scope.ServiceProvider.GetRequiredService<TestLanguage>().Current = language;
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
}

/// <summary>A settable <see cref="IRequestLanguage"/>: the tests' way of saying "asked in Vietnamese".</summary>
public class TestLanguage : IRequestLanguage
{
    public string Current { get; set; } = "vi";
}
