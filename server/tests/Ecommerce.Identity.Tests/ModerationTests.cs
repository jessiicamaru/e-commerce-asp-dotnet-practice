using Ecommerce.Application.Auth.Commands.Login;
using Ecommerce.Application.Auth.Commands.Refresh;
using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Application.Users;
using Ecommerce.Contracts.Activity;
using Ecommerce.Domain.Constants;
using Ecommerce.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Notifications;
using FluentValidation;
using MassTransit.Testing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// Moderators, locks and bans (specs/043): who may do what to whom, and that a stopped account really
/// cannot get in - by password or by refresh.
/// </summary>
[Collection(nameof(IdentityTestCollection))]
public class ModerationTests(IdentityTestFixture fixture)
{
    private const string Password = "Passw0rd!23";
    private readonly IdentityTestFixture _fixture = fixture;
    private static readonly Guid Admin = Guid.CreateVersion7();

    [Fact]
    public async Task An_administrator_grants_moderator_and_it_arrives_at_the_next_refresh()
    {
        var (id, email) = await NewUserAsync();
        var session = await SendAsync(Guid.Empty, new LoginCommand(email, Password));

        var granted = await SendAsync(Admin, new GrantRoleCommand(id, RoleNames.Moderator), RoleNames.Admin);
        Assert.Contains(RoleNames.Moderator, granted.Roles);

        var refreshed = await SendAsync(Guid.Empty, new RefreshTokenCommand(session.RefreshToken));
        Assert.Contains(RoleNames.Moderator, refreshed.Roles);

        await SendAsync(Admin, new RevokeRoleCommand(id, RoleNames.Moderator), RoleNames.Admin);
        var again = await SendAsync(Guid.Empty, new RefreshTokenCommand(refreshed.RefreshToken));
        Assert.DoesNotContain(RoleNames.Moderator, again.Roles);
    }

    [Fact]
    public async Task Moderator_is_the_only_role_that_can_be_granted()
    {
        var (id, _) = await NewUserAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            SendAsync(Admin, new GrantRoleCommand(id, RoleNames.Admin), RoleNames.Admin));
    }

    [Fact]
    public async Task A_locked_account_cannot_sign_in_or_refresh_until_it_is_unlocked()
    {
        var (id, email) = await NewUserAsync();
        var session = await SendAsync(Guid.Empty, new LoginCommand(email, Password));

        var locked = await SendAsync(Admin, new LockUserCommand(id, 3, "Spam in reviews"), RoleNames.Moderator);
        Assert.NotNull(locked.LockedUntil);

        var refused = await Assert.ThrowsAsync<ForbiddenException>(() => SendAsync(Guid.Empty, new LoginCommand(email, Password)));
        Assert.Contains("Spam in reviews", refused.Message);
        // The facts beside the sentence, for the storefront to word in its reader's language (specs/049).
        Assert.Equal("AccountLocked", refused.Facts["code"]);
        Assert.Equal("Spam in reviews", refused.Facts["reason"]);
        var until = Assert.IsType<DateTime>(refused.Facts["until"]);
        Assert.Equal(DateTimeKind.Utc, until.Kind);
        Assert.Equal(locked.LockedUntil!.Value, until, TimeSpan.FromMilliseconds(1)); // PostgreSQL keeps microseconds
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => SendAsync(Guid.Empty, new RefreshTokenCommand(session.RefreshToken)));

        // The wrong password still says only "wrong": the reason is for the account's owner (#28).
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => SendAsync(Guid.Empty, new LoginCommand(email, "Wrong-Passw0rd")));

        await SendAsync(Admin, new UnlockUserCommand(id), RoleNames.Moderator);
        await SendAsync(Guid.Empty, new LoginCommand(email, Password));
    }

    [Fact]
    public async Task A_moderator_locks_for_at_most_thirty_days_and_an_administrator_for_longer()
    {
        var (id, _) = await NewUserAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            SendAsync(Admin, new LockUserCommand(id, 31, "Too long"), RoleNames.Moderator));
        await SendAsync(Admin, new LockUserCommand(id, 30, "Just right"), RoleNames.Moderator);
        var longer = await SendAsync(Admin, new LockUserCommand(id, 90, "Admin may"), RoleNames.Admin);

        Assert.True(longer.LockedUntil > DateTime.UtcNow.AddDays(89));
    }

    [Fact]
    public async Task Nobody_stops_themselves_or_an_administrator_and_a_moderator_does_not_stop_a_moderator()
    {
        var (admin, _) = await NewUserAsync(RoleNames.Admin);
        var (moderator, _) = await NewUserAsync(RoleNames.Moderator);
        var (other, _) = await NewUserAsync(RoleNames.Moderator);

        await Assert.ThrowsAsync<ConflictException>(() =>
            SendAsync(moderator, new LockUserCommand(moderator, 1, "Myself"), RoleNames.Moderator));
        await Assert.ThrowsAsync<ConflictException>(() =>
            SendAsync(Admin, new BanUserCommand(admin, "Another admin"), RoleNames.Admin));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            SendAsync(moderator, new LockUserCommand(other, 1, "A colleague"), RoleNames.Moderator));

        // An administrator may stop a moderator.
        await SendAsync(Admin, new LockUserCommand(other, 1, "Admin decides"), RoleNames.Admin);
    }

    [Fact]
    public async Task A_ban_holds_until_lifted()
    {
        var (id, email) = await NewUserAsync();

        await SendAsync(Admin, new BanUserCommand(id, "Fraud"), RoleNames.Admin);
        var refused = await Assert.ThrowsAsync<ForbiddenException>(() => SendAsync(Guid.Empty, new LoginCommand(email, Password)));
        Assert.Contains("banned", refused.Message);
        Assert.Equal(("AccountBanned", "Fraud"), (refused.Facts["code"], refused.Facts["reason"]));
        Assert.False(refused.Facts.ContainsKey("until"));

        // Unlocking is not lifting a ban.
        await SendAsync(Admin, new UnlockUserCommand(id), RoleNames.Moderator);
        await Assert.ThrowsAsync<ForbiddenException>(() => SendAsync(Guid.Empty, new LoginCommand(email, Password)));

        await SendAsync(Admin, new LiftBanCommand(id), RoleNames.Admin);
        await SendAsync(Guid.Empty, new LoginCommand(email, Password));
    }

    [Fact]
    public async Task Every_action_is_on_the_record_with_its_diff_and_a_grant_tells_the_person()
    {
        var (id, _) = await NewUserAsync();

        var (audit, notices) = await PublishedAsync(Admin, new GrantRoleCommand(id, RoleNames.Moderator), RoleNames.Admin);
        var entry = Assert.Single(audit);
        Assert.Equal(("Security", "RoleGranted"), (entry.Category, entry.Action));
        Assert.DoesNotContain(RoleNames.Moderator, entry.Before);
        Assert.Contains(RoleNames.Moderator, entry.After);
        Assert.Equal((id, "ModeratorGranted"), (Assert.Single(notices).RecipientId, notices[0].Kind));

        (_, notices) = await PublishedAsync(Admin, new RevokeRoleCommand(id, RoleNames.Moderator), RoleNames.Admin);
        Assert.Equal((id, "ModeratorRevoked"), (Assert.Single(notices).RecipientId, notices[0].Kind));

        (audit, _) = await PublishedAsync(Admin, new LockUserCommand(id, 2, "Cooling off"), RoleNames.Admin);
        Assert.Equal(("Moderation", "AccountLocked"), (Assert.Single(audit).Category, audit[0].Action));
        Assert.Contains("Cooling off", audit[0].After);
    }

    [Fact]
    public async Task Staff_find_people_by_part_of_their_email()
    {
        var (id, email) = await NewUserAsync();

        var page = await SendAsync(Admin, new GetUsersQuery(email[..14].ToUpperInvariant()), RoleNames.Moderator);

        Assert.Equal(id, Assert.Single(page.Items).Id);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<(Guid Id, string Email)> NewUserAsync(params string[] roles)
    {
        var email = $"mod-{Guid.NewGuid():N}@example.test";
        var registered = await SendAsync(Guid.Empty, new RegisterCommand(email, Password, "Test", "Person"));

        if (roles.Length > 0)
        {
            await using var provider = _fixture.For(Guid.Empty);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.Include(u => u.Roles).SingleAsync(u => u.Id == registered.Id);
            foreach (var role in await db.Roles.Where(r => roles.Contains(r.Name)).ToListAsync())
                user.Roles.Add(role);
            await db.SaveChangesAsync();
        }

        return (registered.Id, email);
    }

    private async Task<T> SendAsync<T>(Guid caller, IRequest<T> request, params string[] roles)
    {
        await using var provider = _fixture.For(caller, roles);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task<(List<AuditEntryRecorded>, List<UserNotificationRequested>)> PublishedAsync<T>(
        Guid caller, IRequest<T> request, params string[] roles)
    {
        await using var provider = _fixture.For(caller, roles);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        await using (var scope = provider.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);

        var notices = harness.Published.Select<UserNotificationRequested>().Select(x => x.Context.Message).ToList();
        Assert.Empty(notices.SelectMany(n => NotificationContract.Problems(n.Kind, n.Data))); // specs/048
        return (harness.Published.Select<AuditEntryRecorded>().Select(x => x.Context.Message).ToList(), notices);
    }
}
