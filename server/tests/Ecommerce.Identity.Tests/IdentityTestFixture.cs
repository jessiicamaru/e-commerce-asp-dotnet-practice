using Ecommerce.Application;
using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Domain.Entities;
using Ecommerce.Infrastructure.Persistence;
using Ecommerce.Infrastructure.Persistence.Repositories;
using Ecommerce.Shared.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// The real Application stack against a throwaway database on Identity's PostgreSQL (5435).
/// </summary>
/// <remarks>
/// Not faked on purpose (constitution V): "exactly one default address" is a partial unique index plus
/// a row lock, and "not yours" is a WHERE clause - guarantees only a real database evaluates.
/// </remarks>
public class IdentityTestFixture : IAsyncLifetime
{
    private string _databaseName = null!;
    private string _connectionString = null!;
    private string _adminConnectionString = null!;

    public async Task InitializeAsync()
    {
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "123456";
        _databaseName = $"identity_tests_{Guid.NewGuid():N}";
        _adminConnectionString = $"Host=localhost;Port=5435;Database=postgres;Username=postgres;Password={password}";
        _connectionString =
            $"Host=localhost;Port=5435;Database={_databaseName};Username=postgres;"
            + $"Password={password};Maximum Pool Size=30;Timeout=30;Command Timeout=60";

        await using (var admin = new NpgsqlConnection(_adminConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        await using var scope = For(Guid.Empty).CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
    }

    /// <summary>A provider whose caller is <paramref name="userId"/>.</summary>
    public ServiceProvider For(Guid userId)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(_connectionString));
        services.AddScoped<IAddressRepository, AddressRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<ICurrentUser>(new FixedUser(userId));
        return services.BuildServiceProvider(validateScopes: true);
    }

    /// <summary>A customer row to own addresses - the address book is locked through it.</summary>
    public async Task<Guid> NewCustomerAsync()
    {
        var id = Guid.CreateVersion7();
        await using var provider = For(id);
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Users.Add(new User
        {
            Id = id,
            Email = $"{id:N}@example.test",
            PasswordHash = "not-a-real-hash",
            FirstName = "Test",
            LastName = "Customer"
        });
        await context.SaveChangesAsync();
        return id;
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }

    private sealed class FixedUser(Guid id) : ICurrentUser
    {
        public Guid? Id { get; } = id;
        public string? Email => null;
        public bool IsAuthenticated => true;
    }
}

[CollectionDefinition(nameof(IdentityTestCollection))]
public class IdentityTestCollection : ICollectionFixture<IdentityTestFixture>;
