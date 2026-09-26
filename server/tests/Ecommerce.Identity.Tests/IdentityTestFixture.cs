using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Notifications;
using MassTransit;
using Ecommerce.Application;
using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Domain.Entities;
using Ecommerce.Infrastructure.Persistence;
using Ecommerce.Infrastructure.Persistence.Repositories;
using Ecommerce.Infrastructure.Security;
using Ecommerce.Domain.Constants;
using Ecommerce.Shared.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

using Ecommerce.Application.Email;
using Ecommerce.Infrastructure.Email;

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
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        // EVERY role the system ships with, from the same list DataInitializer seeds from.
        //
        // It used to be one hand-written Customer row, and specs/027 added Seller to RoleNames
        // without the fixture hearing about it: three tests failed with "The 'Seller' role is
        // missing" and the next role would have done the same. Reading the list means a role added
        // to the service is a role the tests already have.
        db.Roles.AddRange(RoleNames.Descriptions.Select(pair => new Role
        {
            Id = Guid.CreateVersion7(),
            Name = pair.Key,
            Description = pair.Value
        }));
        await db.SaveChangesAsync();
    }

    /// <summary>What the dispatcher would have sent - and a switch to make the "mail server" refuse.</summary>
    public FakeEmailTransport Mail { get; } = new();

    /// <summary>A provider whose caller is <paramref name="userId"/>.</summary>
    public ServiceProvider For(Guid userId, params string[] roles)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        // With retries on, as in production: a retrying strategy refuses a hand-opened transaction, and a
        // fixture without it passed code that answered every refresh with 500 (found building #29).
        services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(_connectionString, npgsql => npgsql.EnableRetryOnFailure()));
        services.AddScoped<IAddressRepository, AddressRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Sign-up and sign-in (issue #28) - the real repositories, hasher and token generator.
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IShopApplicationRepository, ShopApplicationRepository>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new JwtSettings
        {
            Secret = "identity-tests-signing-key-of-at-least-32-bytes",
            Issuer = "EcommerceApi",
            Audience = "EcommerceClients",
            ExpiryMinutes = 15,
            RefreshTokenExpiryDays = 7
        }));
        // RegisterSellerCommand publishes SellerRegisteredEvent through the outbox (specs/027), so
        // the fixture needs a bus. In-memory: the assertions here are about what Identity STORES and
        // returns - that Catalog hears about it is verified where Catalog consumes it.
        services.AddMassTransitTestHarness();
        services.AddAuditTrail("identity");
        services.AddNotifier();

        // Email (specs/060): the real queue and dispatcher, and a transport that records instead of sending.
        services.AddScoped<IOutgoingEmailRepository, OutgoingEmailRepository>();
        services.AddScoped<IEmailTemplateStore, EmailTemplateStore>();
        services.AddSingleton<IHtmlSanitizer, AllowListHtmlSanitizer>();
        services.AddScoped<Ecommerce.Application.Auth.Commands.PasswordReset.IPasswordResetRepository, PasswordResetRepository>();
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new Ecommerce.Application.Auth.SignInThrottling.SignInOptions()));
        services.AddScoped<Ecommerce.Application.Auth.SignInThrottling.ISignInThrottle, SignInThrottleRepository>();
        services.AddScoped<Ecommerce.Application.Auth.Commands.EmailConfirmation.IEmailConfirmationRepository, EmailConfirmationRepository>();
        services.AddSingleton<IEmailTransport>(Mail);
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new EmailOptions { StorefrontUrl = "http://shop.test" }));

        services.AddSingleton<ICurrentUser>(new FixedUser(userId, roles));
        return services.BuildServiceProvider(validateScopes: true);
    }

    /// <summary>
    /// Registers somebody who sells and approves their application as a moderator would (specs/044) -
    /// what a seller is now. Returns the registration's response; roles on it are the applicant's.
    /// </summary>
    public async Task<Ecommerce.Application.Auth.Common.AuthResponse> ApprovedSellerAsync(string email, string shopName, string password = "Passw0rd!23")
    {
        Ecommerce.Application.Auth.Common.AuthResponse registered;
        await using (var provider = For(Guid.Empty))
        await using (var scope = provider.CreateAsyncScope())
        {
            registered = await scope.ServiceProvider.GetRequiredService<MediatR.ISender>().Send(
                new Ecommerce.Application.Auth.Commands.RegisterSeller.RegisterSellerCommand(email, password, "Test", "Seller", shopName));
        }

        // A shop is approved only for a confirmed address (specs/063).
        await ConfirmEmailAsync(registered.Id);

        Guid applicationId;
        await using (var provider = For(registered.Id))
        await using (var scope = provider.CreateAsyncScope())
        {
            var mine = await scope.ServiceProvider.GetRequiredService<MediatR.ISender>().Send(
                new Ecommerce.Application.ShopApplications.GetMyShopApplicationsQuery());
            applicationId = mine.Single().Id;
        }

        await using (var provider = For(Guid.CreateVersion7(), RoleNames.Moderator))
        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<MediatR.ISender>().Send(
                new Ecommerce.Application.ShopApplications.ApproveShopApplicationCommand(applicationId));
        }

        return registered;
    }

    /// <summary>
    /// Marks an address confirmed as its link would (specs/063) - for tests about something else that needs a
    /// confirmed account. <c>EmailConfirmationTests</c> use the link itself.
    /// </summary>
    public async Task ConfirmEmailAsync(Guid userId)
    {
        await using var provider = For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.EmailConfirmedAt, DateTime.UtcNow));
    }

    /// <summary>
    /// Removes the confirmation email registering queued (specs/063), for tests that count the other emails a
    /// person gets.
    /// </summary>
    public async Task DropConfirmationEmailAsync(Guid userId)
    {
        await using var provider = For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutgoingEmails
            .Where(e => e.RecipientId == userId && e.Template == "EmailConfirmation").ExecuteDeleteAsync();
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

    /// <summary>Moves a rotation into the past, beyond the reuse grace window, as a later replay would find it.</summary>
    public async Task AgeRevocationAsync(string token)
    {
        await using var provider = For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().RefreshTokens.Where(t => t.Token == token)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow.AddMinutes(-5)));
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }

    private sealed class FixedUser(Guid id, string[] roles) : ICurrentUser
    {
        public Guid? Id { get; } = id;
        public string? Email => null;
        public bool IsAuthenticated => true;

        // The roles the token would carry - the moderation rules ask (specs/043).
        public bool IsInRole(string role) => roles.Contains(role);
    }
}

[CollectionDefinition(nameof(IdentityTestCollection))]
public class IdentityTestCollection : ICollectionFixture<IdentityTestFixture>;

/// <summary>
/// A mail server that records what it was given, and can be switched off (specs/060). <c>Body</c> is the plain-text
/// part, what every assertion before specs/077 read; <c>Html</c> is the HTML part beside it.
/// </summary>
public sealed class FakeEmailTransport : IEmailTransport
{
    private readonly List<(string To, string Subject, string Body, string Html)> _sent = [];

    public bool Down { get; set; }

    public IReadOnlyList<(string To, string Subject, string Body, string Html)> SentTo(string to)
    {
        lock (_sent) return _sent.Where(m => string.Equals(m.To, to, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public Task SendAsync(string to, string subject, string text, string html, CancellationToken cancellationToken = default)
    {
        if (Down)
        {
            throw new InvalidOperationException("Connection refused (the test's mail server is down).");
        }

        lock (_sent) _sent.Add((to, subject, text, html));
        return Task.CompletedTask;
    }
}
