using Ecommerce.Application.Auth.Commands.Login;
using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Domain.Entities;
using Ecommerce.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// One mailbox, one account, whatever the case it is typed in (issue #49). <c>Case@Example.test</c> and
/// <c>case@example.test</c> used to register as two accounts.
/// </summary>
[Collection(nameof(IdentityTestCollection))]
public class EmailCaseTests(IdentityTestFixture fixture)
{
    private readonly IdentityTestFixture _fixture = fixture;

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private static string NewLocalPart() => $"Case{Guid.NewGuid():N}";

    [Fact]
    public async Task Registering_the_same_mailbox_in_another_case_is_a_conflict()
    {
        var local = NewLocalPart();
        await SendAsync(new RegisterCommand($"{local}@Example.test", "Passw0rd!23", "A", "B"));

        await Assert.ThrowsAsync<ConflictException>(() =>
            SendAsync(new RegisterCommand($"{local.ToLowerInvariant()}@example.test", "Other-Passw0rd", "C", "D")));
    }

    [Fact]
    public async Task Signing_in_works_whatever_the_case_and_the_email_is_kept_as_typed()
    {
        var typed = $"{NewLocalPart()}@Example.test";
        await SendAsync(new RegisterCommand(typed, "Passw0rd!23", "A", "B"));

        var lower = await SendAsync(new LoginCommand(typed.ToLowerInvariant(), "Passw0rd!23"));
        var upper = await SendAsync(new LoginCommand($"  {typed.ToUpperInvariant()} ", "Passw0rd!23"));

        Assert.Equal(lower.Id, upper.Id);
        Assert.Equal(typed, lower.Email);
    }

    [Fact]
    public async Task The_database_refuses_a_case_only_duplicate_even_without_the_application()
    {
        var local = NewLocalPart();

        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.Users.Add(NewUser($"{local}@example.test"));
        await db.SaveChangesAsync();
        db.Users.Add(NewUser($"{local.ToUpperInvariant()}@EXAMPLE.TEST"));

        var refused = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("IX_users_Email_lower", Assert.IsType<PostgresException>(refused.InnerException).ConstraintName);
    }

    [Fact]
    public async Task Existing_case_only_duplicates_stop_the_migration_without_logging_the_addresses()
    {
        // A database of its own, migrated to the version before, with the duplicate this issue found.
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "123456";
        var name = $"identity_migration_{Guid.NewGuid():N}";
        var admin = $"Host=localhost;Port=5435;Database=postgres;Username=postgres;Password={password}";
        var connection = $"Host=localhost;Port=5435;Database={name};Username=postgres;Password={password}";

        await using (var conn = new NpgsqlConnection(admin))
        {
            await conn.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE \"{name}\"", conn).ExecuteNonQueryAsync();
        }

        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options;
            await using var db = new ApplicationDbContext(options);
            var migrator = db.GetService<IMigrator>();

            await migrator.MigrateAsync("20260921153902_AddDeliveryAddresses");
            db.Users.AddRange(NewUser("Twin@Example.test"), NewUser("twin@example.test"));
            await db.SaveChangesAsync();

            var refused = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync());

            Assert.Contains("1 email(s) belong to more than one account", refused.MessageText);
            Assert.DoesNotContain("twin", refused.MessageText, StringComparison.OrdinalIgnoreCase);
            // Nothing half-applied: the index does not exist, and the two accounts are untouched.
            Assert.Equal(2, await db.Users.CountAsync());
        }
        finally
        {
            await using var conn = new NpgsqlConnection(admin);
            await conn.OpenAsync();
            await new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", conn).ExecuteNonQueryAsync();
        }
    }

    private static User NewUser(string email) => new()
    {
        Id = Guid.CreateVersion7(),
        Email = email,
        PasswordHash = "not-a-real-hash",
        FirstName = "A",
        LastName = "B"
    };
}
