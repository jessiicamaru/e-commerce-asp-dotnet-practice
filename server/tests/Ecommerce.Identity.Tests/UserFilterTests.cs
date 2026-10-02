using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Application.Users;
using Ecommerce.Domain.Constants;
using Ecommerce.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// The people list by role and by state (specs/133, #249). Each test searches a name of its own, so the database the
/// collection shares cannot add to what it counts.
/// </summary>
[Collection(nameof(IdentityTestCollection))]
public class UserFilterTests(IdentityTestFixture fixture)
{
    private readonly IdentityTestFixture _fixture = fixture;
    private static readonly Guid Admin = Guid.CreateVersion7();

    [Fact]
    public async Task A_role_lists_whoever_holds_it()
    {
        var tag = $"role{Guid.NewGuid():N}"[..12];
        var moderator = await PersonAsync(tag, roles: [RoleNames.Moderator]);
        await PersonAsync(tag);

        var moderators = await ListAsync(tag, role: RoleNames.Moderator);
        Assert.Equal([moderator], moderators.Items.Select(u => u.Id));
        Assert.Equal(2, (await ListAsync(tag, role: RoleNames.Customer)).TotalCount);
    }

    [Fact]
    public async Task A_state_lists_the_active_the_locked_and_the_banned_apart()
    {
        var tag = $"state{Guid.NewGuid():N}"[..13];
        var active = await PersonAsync(tag);
        var lapsed = await PersonAsync(tag, lockedUntil: DateTime.UtcNow.AddDays(-1));
        var locked = await PersonAsync(tag, lockedUntil: DateTime.UtcNow.AddDays(3));
        var banned = await PersonAsync(tag, banned: true);

        Assert.Equal([active, lapsed], (await ListAsync(tag, state: UserListState.Active)).Items.Select(u => u.Id).Order());
        Assert.Equal([locked], (await ListAsync(tag, state: UserListState.Locked)).Items.Select(u => u.Id));
        Assert.Equal([banned], (await ListAsync(tag, state: UserListState.Banned)).Items.Select(u => u.Id));
    }

    [Fact]
    public async Task An_unknown_role_or_state_is_refused()
    {
        var role = await Assert.ThrowsAsync<ValidationException>(() => ListAsync("x", role: "Owner"));
        Assert.Contains(role.Errors, e => e.PropertyName == "Role");
        var state = await Assert.ThrowsAsync<ValidationException>(() => ListAsync("x", state: "Suspended"));
        Assert.Contains(state.Errors, e => e.PropertyName == "State");
    }

    private async Task<Guid> PersonAsync(string tag, string[]? roles = null, DateTime? lockedUntil = null, bool banned = false)
    {
        var email = $"{tag}-{Guid.NewGuid():N}@example.test";
        Guid id;
        await using (var provider = _fixture.For(Guid.Empty))
        await using (var scope = provider.CreateAsyncScope())
            id = (await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RegisterCommand(email, "Passw0rd!23", "Test", tag))).Id;

        await using var p = _fixture.For(Guid.Empty);
        await using var s = p.CreateAsyncScope();
        var db = s.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.Include(u => u.Roles).SingleAsync(u => u.Id == id);
        foreach (var role in await db.Roles.Where(r => (roles ?? Array.Empty<string>()).Contains(r.Name)).ToListAsync())
            user.Roles.Add(role);
        user.LockedUntil = lockedUntil;
        if (banned)
        {
            user.BannedAt = DateTime.UtcNow;
            user.BanReason = "Fraud";
        }
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<UserAdminPage> ListAsync(string search, string? role = null, string? state = null)
    {
        await using var provider = _fixture.For(Admin, RoleNames.Admin);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetUsersQuery(search, 1, 50, Role: role, State: state));
    }
}
