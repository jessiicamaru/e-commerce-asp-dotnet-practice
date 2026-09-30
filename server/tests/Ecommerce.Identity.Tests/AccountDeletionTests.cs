using System.Text.Json;
using Ecommerce.Application.Auth.Commands.DeleteAccount;
using Ecommerce.Application.Auth.Commands.Login;
using Ecommerce.Application.Auth.Commands.Refresh;
using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Application.MyData;
using Ecommerce.Contracts.Activity;
using Ecommerce.Contracts.Identity;
using Ecommerce.Domain.Entities;
using Ecommerce.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MassTransit.Testing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// A person deletes their account (specs/112, #217): the row is emptied, everything else Identity holds goes, every
/// service is told and every session ends - and nothing changes when the password is wrong, the account is staff's,
/// business is still open, or Order cannot be asked.
/// </summary>
[Collection(nameof(IdentityTestCollection))]
public class AccountDeletionTests : IDisposable
{
    private const string Password = "Passw0rd!23";
    private readonly IdentityTestFixture _fixture;

    public AccountDeletionTests(IdentityTestFixture fixture)
    {
        _fixture = fixture;
        _fixture.Standing.Reset();
    }

    public void Dispose()
    {
        _fixture.Standing.Reset();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Deleting_empties_the_account_erases_the_rest_and_tells_every_service()
    {
        var mai = await PersonWithEverythingAsync("Mai");

        var published = await PublishedAsync(mai.Id, new DeleteAccountCommand(Password), "Customer");

        var export = await SendAsync(mai.Id, new GetMyDataQuery());
        foreach (var section in IdentityPersonalData.Inventory.Erased)
            Assert.Empty(export.Sections[section]);
        var json = JsonSerializer.Serialize(export);
        Assert.DoesNotContain(mai.Email, json);
        Assert.DoesNotContain("Mai Street 1", json);
        Assert.DoesNotContain("0912 345 678", json);
        Assert.DoesNotContain("\"FirstName\":\"Mai\"", json);

        var row = await DbAsync(db => db.Users.Include(u => u.Roles).AsNoTracking().SingleAsync(u => u.Id == mai.Id));
        Assert.Equal(AccountDeletion.PlaceholderEmail(mai.Id), row.Email);
        Assert.NotNull(row.DeletedAt);
        Assert.Empty(row.Roles);
        Assert.Null(row.TwoFactorSecret);
        Assert.False(await DbAsync(db => db.RefreshTokens.AnyAsync(t => t.UserId == mai.Id)));
        Assert.False(await DbAsync(db => db.EmailConfirmationTokens.AnyAsync(t => t.UserId == mai.Id)));
        Assert.False(await DbAsync(db => db.TwoFactorRecoveryCodes.AnyAsync(c => c.UserId == mai.Id)));

        var deleted = Assert.Single(published.OfType<AccountDeleted>());
        Assert.Equal((mai.Id, mai.Email), (deleted.UserId, deleted.Email));
        Assert.Contains(published.OfType<AccessTokensRevoked>(), r => r.UserId == mai.Id && r.Reason == "AccountDeleted");
        var entry = Assert.Single(published.OfType<AuditEntryRecorded>(), a => a.Action == "AccountDeleted");
        Assert.DoesNotContain(mai.Email, JsonSerializer.Serialize(entry));
    }

    [Fact]
    public async Task Afterwards_the_old_password_signs_nobody_in_the_session_is_gone_and_the_email_registers_again()
    {
        var mai = await PersonWithEverythingAsync("Mai");

        await SendAsync(mai.Id, new DeleteAccountCommand(Password), "Customer");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => SendAsync(Guid.Empty, new LoginCommand(mai.Email, Password)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => SendAsync(Guid.Empty, new RefreshTokenCommand(mai.RefreshToken)));
        var again = await SendAsync(Guid.Empty, new RegisterCommand(mai.Email, Password, "Mai", "Again"));
        Assert.NotEqual(mai.Id, again.Id);
        Assert.Empty(await DbAsync(db => db.DeliveryAddresses.Where(a => a.UserId == again.Id).ToListAsync()));
    }

    [Fact]
    public async Task A_wrong_password_changes_nothing_asks_nobody_and_counts_toward_the_pause()
    {
        var mai = await PersonWithEverythingAsync("Mai");

        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(mai.Id, new DeleteAccountCommand("not-it"), "Customer"));

        await AssertUntouchedAsync(mai);
        Assert.Equal(0, _fixture.Standing.Asked);
        var key = Ecommerce.Application.Common.EmailKey.For(mai.Email);
        Assert.Equal(1, (await DbAsync(db => db.SignInThrottles.AsNoTracking().SingleAsync(t => t.EmailKey == key))).Failures);
    }

    [Theory]
    [InlineData("Moderator")]
    [InlineData("Admin")]
    public async Task A_staff_account_is_refused_and_changes_nothing(string role)
    {
        var mai = await PersonWithEverythingAsync("Mai");
        await DbAsync(async db =>
        {
            var user = await db.Users.Include(u => u.Roles).SingleAsync(u => u.Id == mai.Id);
            user.Roles.Add(await db.Roles.SingleAsync(r => r.Name == role));
            return await db.SaveChangesAsync();
        });

        var refused = await Assert.ThrowsAsync<ConflictException>(() => SendAsync(mai.Id, new DeleteAccountCommand(Password), role));

        Assert.Equal(AccountDeletion.StaffAccount, refused.Facts["code"]);
        await AssertUntouchedAsync(mai);
    }

    [Fact]
    public async Task Open_business_is_refused_with_each_reason_and_changes_nothing()
    {
        var mai = await PersonWithEverythingAsync("Mai");
        _fixture.Standing.Blockers.AddRange(["OpenOrders", "UnpaidEarnings"]);

        var refused = await Assert.ThrowsAsync<ConflictException>(() => SendAsync(mai.Id, new DeleteAccountCommand(Password), "Customer"));

        Assert.Equal(AccountDeletion.OpenBusiness, refused.Facts["code"]);
        Assert.Equal(["OpenOrders", "UnpaidEarnings"], (IEnumerable<string>)refused.Facts["reasons"]!);
        await AssertUntouchedAsync(mai);
    }

    [Fact]
    public async Task Order_unreachable_is_503_and_changes_nothing()
    {
        var mai = await PersonWithEverythingAsync("Mai");
        _fixture.Standing.Unreachable = true;

        await Assert.ThrowsAsync<DependencyUnavailableException>(() => SendAsync(mai.Id, new DeleteAccountCommand(Password), "Customer"));

        await AssertUntouchedAsync(mai);
    }

    // ------------------------------------------------------------------ helpers

    private sealed record Person(Guid Id, string Email, string RefreshToken);

    /// <summary>A person signed in, with a row in every table of Identity's inventory that names them.</summary>
    private async Task<Person> PersonWithEverythingAsync(string name)
    {
        var email = $"delete-{name.ToLowerInvariant()}-{Guid.NewGuid():N}@example.test";
        var id = (await SendAsync(Guid.Empty, new RegisterCommand(email, Password, name, "Nguyen"))).Id;
        var session = await SendAsync(Guid.Empty, new LoginCommand(email, Password));
        var now = DateTime.UtcNow;

        await DbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == id);
            user.PhoneNumber = "0912 345 678";
            user.TwoFactorSecret = "TOTP-SECRET-SEALED";
            db.DeliveryAddresses.Add(new DeliveryAddress
            {
                Id = Guid.CreateVersion7(), UserId = id, RecipientName = name, Line1 = $"{name} Street 1", City = "Hanoi",
                PostalCode = "100000", Country = "VN", CreatedAt = now, UpdatedAt = now,
            });
            db.SellerProfiles.Add(new SellerProfile { UserId = id, ShopName = $"{name} Lens", CreatedAt = now, UpdatedAt = now });
            db.SellerPayoutAccounts.Add(new SellerPayoutAccount { SellerId = id, BankName = "Vietcombank", AccountHolder = name, AccountNumber = "9704123456789012", UpdatedAt = now });
            db.ShopApplications.Add(new ShopApplication { Id = Guid.CreateVersion7(), UserId = id, ShopName = $"{name} Lens", CreatedAt = now });
            db.TwoFactorRecoveryCodes.Add(new TwoFactorRecoveryCode { Id = Guid.CreateVersion7(), UserId = id, CodeHash = "hash", CreatedAt = now });
            return await db.SaveChangesAsync();
        });

        return new Person(id, email, session.RefreshToken);
    }

    /// <summary>Nothing moved: the email, the address and the session are all still there.</summary>
    private async Task AssertUntouchedAsync(Person person)
    {
        var row = await DbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == person.Id));
        Assert.Equal(person.Email, row.Email);
        Assert.Null(row.DeletedAt);
        Assert.True(await DbAsync(db => db.DeliveryAddresses.AnyAsync(a => a.UserId == person.Id)));
        Assert.True(await DbAsync(db => db.RefreshTokens.AnyAsync(t => t.UserId == person.Id && t.RevokedAt == null)));
    }

    private async Task<T> DbAsync<T>(Func<ApplicationDbContext, Task<T>> work)
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private async Task<T> SendAsync<T>(Guid caller, IRequest<T> request)
    {
        await using var provider = _fixture.For(caller);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task SendAsync(Guid caller, IRequest request, params string[] roles)
    {
        await using var provider = _fixture.For(caller, roles);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task<List<object>> PublishedAsync(Guid caller, IRequest request, params string[] roles)
    {
        await using var provider = _fixture.For(caller, roles);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
        return harness.Published.Select(_ => true).Select(m => m.MessageObject).ToList();
    }
}
