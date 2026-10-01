using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Application.Users;
using Ecommerce.Domain.Constants;
using Ecommerce.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// Staff and a deleted account (specs/123, #241): the list leaves it out unless asked, says when it was deleted, and
/// every moderation command refuses it - it was listed as Active with the menu to lock, ban or grant a role.
/// </summary>
[Collection(nameof(IdentityTestCollection))]
public class DeletedAccountStaffTests(IdentityTestFixture fixture)
{
    private readonly IdentityTestFixture _fixture = fixture;
    private static readonly Guid Admin = Guid.CreateVersion7();

    [Fact]
    public async Task The_users_list_leaves_a_deleted_account_out_unless_asked()
    {
        var (live, liveEmail) = await NewUserAsync();
        var (gone, goneEmail) = await NewDeletedUserAsync();

        var byDefault = await SendAsync(Admin, new GetUsersQuery(goneEmail), RoleNames.Admin);
        Assert.Empty(byDefault.Items);
        Assert.Equal(0, byDefault.TotalCount);

        var asked = await SendAsync(Admin, new GetUsersQuery(goneEmail, IncludeDeleted: true), RoleNames.Admin);
        var row = Assert.Single(asked.Items);
        Assert.Equal(gone, row.Id);
        Assert.NotNull(row.DeletedAt);

        // The control: a live account is listed either way, with no deletion date.
        foreach (var include in new[] { false, true })
        {
            var found = Assert.Single((await SendAsync(Admin, new GetUsersQuery(liveEmail, IncludeDeleted: include), RoleNames.Admin)).Items);
            Assert.Equal(live, found.Id);
            Assert.Null(found.DeletedAt);
        }
    }

    public static TheoryData<string> Commands => new() { "lock", "unlock", "ban", "lift", "grant", "revoke" };

    [Theory]
    [MemberData(nameof(Commands))]
    public async Task Every_moderation_command_refuses_a_deleted_account_and_writes_nothing(string command)
    {
        var (gone, _) = await NewDeletedUserAsync();
        IRequest<UserAdminResponse> request = command switch
        {
            "lock" => new LockUserCommand(gone, 3, "Spam"),
            "unlock" => new UnlockUserCommand(gone),
            "ban" => new BanUserCommand(gone, "Fraud"),
            "lift" => new LiftBanCommand(gone),
            "grant" => new GrantRoleCommand(gone, RoleNames.Moderator),
            _ => new RevokeRoleCommand(gone, RoleNames.Moderator),
        };

        var refused = await Assert.ThrowsAsync<ConflictException>(() => SendAsync(Admin, request, RoleNames.Admin));
        Assert.Equal("AccountDeleted", refused.Facts["code"]);

        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users
            .Include(u => u.Roles).AsNoTracking().SingleAsync(u => u.Id == gone);
        Assert.Null(user.LockedUntil);
        Assert.Null(user.BannedAt);
        Assert.Empty(user.Roles);
    }

    private async Task<(Guid Id, string Email)> NewUserAsync()
    {
        var email = $"deleted-staff-{Guid.NewGuid():N}@example.test";
        var registered = await SendAsync(Guid.Empty, new RegisterCommand(email, "Passw0rd!23", "Test", "Person"));
        return (registered.Id, email);
    }

    /// <summary>An account emptied the way specs/112's deletion leaves it: the address replaced, no roles, a date.</summary>
    private async Task<(Guid Id, string Email)> NewDeletedUserAsync()
    {
        var (id, _) = await NewUserAsync();
        var email = $"deleted-{id:N}@deleted.invalid";

        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.Include(u => u.Roles).SingleAsync(u => u.Id == id);
        user.Email = email;
        user.FirstName = "";
        user.LastName = "";
        user.Roles.Clear();
        user.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (id, email);
    }

    private async Task<T> SendAsync<T>(Guid caller, IRequest<T> request, params string[] roles)
    {
        await using var provider = _fixture.For(caller, roles);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
