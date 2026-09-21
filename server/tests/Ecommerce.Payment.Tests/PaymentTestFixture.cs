using Ecommerce.Payment.Application;
using Ecommerce.Payment.Application.Common.Interfaces;
using Ecommerce.Payment.Infrastructure.Gateway;
using Ecommerce.Payment.Infrastructure.Persistence;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Ecommerce.Payment.Tests;

/// <summary>
/// The real Application stack against a real PostgreSQL, with MassTransit's in-memory harness
/// standing in for the broker.
/// <para>
/// The database is not faked on purpose: the guarantee these tests exist for — one payment per
/// order, however many requests arrive at once — is a unique constraint. An in-memory provider
/// would happily let code that double-charges pass.
/// </para>
/// </summary>
public class PaymentTestFixture : IAsyncLifetime
{
    private const string AdminConnectionString =
        "Host=localhost;Port=5438;Database=postgres;Username=postgres;Password={0}";

    private string _databaseName = null!;
    private string _connectionString = null!;

    public ServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// The throwaway database this fixture created, so a test can build its own provider against
    /// the same data — used by the one that needs a deliberately blind repository.
    /// </summary>
    public string ConnectionString => _connectionString;

    public ITestHarness Harness => Services.GetRequiredService<ITestHarness>();

    public async Task InitializeAsync()
    {
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "123456";

        _databaseName = $"payment_tests_{Guid.NewGuid():N}";

        // The pool is bounded from the start. Feature 001's concurrency test opened one connection
        // per concurrent request, exceeded the server's max_connections, and failed as a connection
        // error rather than on its assertion — while appearing to pass on an earlier run. Npgsql
        // queues the surplus instead, so the requests still race.
        _connectionString =
            $"Host=localhost;Port=5438;Database={_databaseName};Username=postgres;"
            + $"Password={password};Maximum Pool Size=20;Timeout=30;Command Timeout=60";

        await using (var admin = new NpgsqlConnection(string.Format(AdminConnectionString, password)))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        Services = BuildProvider(PaymentOutcomeOptions.ApproveValue);

        await using (var scope = Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            await context.Database.MigrateAsync();
        }

        await Harness.Start();
    }

    /// <summary>
    /// A second container configured to reject, for the rejection tests. It shares the database, so
    /// both outcomes are exercised against the same rows.
    /// </summary>
    public ServiceProvider BuildRejectingProvider() => BuildProvider(PaymentOutcomeOptions.RejectValue);

    private ServiceProvider BuildProvider(string outcome)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["Payment:Outcome"] = outcome
            })
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddApplication();

        services.AddDbContext<PaymentDbContext>(options => options.UseNpgsql(_connectionString));
        services.Configure<PaymentOutcomeOptions>(configuration.GetSection(PaymentOutcomeOptions.SectionName));
        services.AddSingleton<IPaymentGateway, StubPaymentGateway>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();

        services.AddMassTransitTestHarness();

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

[CollectionDefinition(nameof(PaymentTestCollection))]
public class PaymentTestCollection : ICollectionFixture<PaymentTestFixture>;
