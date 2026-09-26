using Ecommerce.Activity.Application;
using Ecommerce.Activity.Application.Common.Interfaces;
using Ecommerce.Activity.Infrastructure;
using Ecommerce.Activity.Infrastructure.Persistence;
using Ecommerce.Activity.Infrastructure.Persistence.Repositories;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Authentication;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Ecommerce.Activity.Tests;

/// <summary>
/// The Activity stack against a real PostgreSQL on 5440, in a throwaway database per run - the guarantee
/// under test (one entry per id, whatever the deliveries) is an INSERT ... ON CONFLICT, which only a
/// database evaluates.
/// </summary>
public class ActivityTestFixture : IAsyncLifetime
{
    private const string AdminConnectionString =
        "Host=localhost;Port=5440;Database=postgres;Username=postgres;Password={0}";

    private string _databaseName = null!;

    public string ConnectionString { get; private set; } = null!;

    public ServiceProvider Services { get; private set; } = null!;

    /// <summary>Whose inbox a request reads - settable per test (specs/042).</summary>
    public TestUser CurrentUser { get; } = new();

    public async Task InitializeAsync()
    {
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "123456";
        _databaseName = $"activity_tests_{Guid.NewGuid():N}";
        ConnectionString = $"Host=localhost;Port=5440;Database={_databaseName};Username=postgres;"
            + $"Password={password};Maximum Pool Size=20;Timeout=30;Command Timeout=60";

        await using (var admin = new NpgsqlConnection(string.Format(AdminConnectionString, password)))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddDbContext<ActivityDbContext>(o => o.UseNpgsql(ConnectionString));
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationWordingStore, NotificationWordingStore>();
        services.AddSingleton<INoticeSanitizer, NoticeSanitizer>();
        // Rewording is audited through the outbox (specs/078); in memory here, and the tests read what was published.
        services.AddMassTransitTestHarness();
        services.AddAuditTrail("activity");
        services.AddSingleton<ICurrentUser>(CurrentUser);
        Services = services.BuildServiceProvider(true);

        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ActivityDbContext>().Database.MigrateAsync();
        await Services.GetRequiredService<MassTransit.Testing.ITestHarness>().Start();
    }

    public async Task DisposeAsync()
    {
        await Services.DisposeAsync();
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "123456";
        await using var admin = new NpgsqlConnection(string.Format(AdminConnectionString, password));
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }

    public AsyncServiceScope NewScope() => Services.CreateAsyncScope();
}

[CollectionDefinition(nameof(ActivityTestCollection))]
public class ActivityTestCollection : ICollectionFixture<ActivityTestFixture>;

public class TestUser : ICurrentUser
{
    public Guid? Id { get; set; }
    public string? Email { get; set; }
    public bool IsAuthenticated => Id is not null;
    public bool IsInRole(string role) => false;
}
