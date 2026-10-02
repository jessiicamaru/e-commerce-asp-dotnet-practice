using System.IdentityModel.Tokens.Jwt;
using Ecommerce.Application.Auth.Commands.PasswordReset;
using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Application.Auth.Handoff;
using Ecommerce.Application.Auth.TwoFactor;
using Ecommerce.Domain.Constants;
using Ecommerce.Domain.Entities;
using Ecommerce.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// A one-time handoff from the storefront to the back office (#279, specs/140): a signed-in staff member's code stands in
/// for the password, never for the second factor. It works once, for 30 seconds, is stored only as its hash, and
/// yields a challenge - the session still takes the authenticator's code, from the back office.
/// </summary>
[Collection(nameof(IdentityTestCollection))]
public class HandoffTests(IdentityTestFixture fixture)
{
    private const string Password = "Passw0rd!23";
    private readonly IdentityTestFixture _fixture = fixture;

    [Fact]
    public async Task A_handoff_yields_a_challenge_and_the_code_makes_a_back_office_session()
    {
        var (id, _, secret) = await EnrolledAsync(RoleNames.Admin);

        var handoff = await SendAsync(id, new IssueBackOfficeHandoffCommand());
        var redeemed = await SendAsync(Guid.Empty, new RedeemBackOfficeHandoffCommand(handoff.Code));

        // A challenge, exactly as the right password gives one - nothing that signs anybody in.
        Assert.Equal(SessionRoles.Required, redeemed.TwoFactor);
        Assert.False(string.IsNullOrEmpty(redeemed.Challenge));
        Assert.Equal(("", ""), (redeemed.Token, redeemed.RefreshToken));
        Assert.Empty(redeemed.Roles);

        var session = await SendAsync(Guid.Empty, new LoginTwoFactorCommand(redeemed.Challenge!, CodeFor(secret), null));
        Assert.Contains(RoleNames.Admin, TokenRoles(session.Token));
    }

    [Fact]
    public async Task A_handoff_works_once()
    {
        var (id, _, _) = await EnrolledAsync(RoleNames.Moderator);
        var handoff = await SendAsync(id, new IssueBackOfficeHandoffCommand());

        await SendAsync(Guid.Empty, new RedeemBackOfficeHandoffCommand(handoff.Code));
        var again = await Assert.ThrowsAsync<ValidationException>(() =>
            SendAsync(Guid.Empty, new RedeemBackOfficeHandoffCommand(handoff.Code)));

        Assert.Contains(again.Errors, e => e.PropertyName == "Code");
    }

    [Fact]
    public async Task An_expired_or_made_up_handoff_is_the_same_refusal()
    {
        var (id, _, _) = await EnrolledAsync(RoleNames.Moderator);
        var handoff = await SendAsync(id, new IssueBackOfficeHandoffCommand());
        await DbAsync(db => db.BackOfficeHandoffs.Where(h => h.UserId == id)
            .ExecuteUpdateAsync(x => x.SetProperty(h => h.ExpiresAt, DateTime.UtcNow.AddSeconds(-1))));

        var expired = await Assert.ThrowsAsync<ValidationException>(() =>
            SendAsync(Guid.Empty, new RedeemBackOfficeHandoffCommand(handoff.Code)));
        var madeUp = await Assert.ThrowsAsync<ValidationException>(() =>
            SendAsync(Guid.Empty, new RedeemBackOfficeHandoffCommand(ResetTokens.NewToken())));

        Assert.Equal(expired.Errors.Single().ErrorMessage, madeUp.Errors.Single().ErrorMessage);
    }

    /// <summary>Thirty seconds: long enough for a click and a page load, no longer.</summary>
    [Fact]
    public async Task A_handoff_lives_thirty_seconds_and_only_its_hash_is_kept()
    {
        var (id, _, _) = await EnrolledAsync(RoleNames.Moderator);
        var before = DateTime.UtcNow;
        var handoff = await SendAsync(id, new IssueBackOfficeHandoffCommand());

        var stored = await ReadAsync(db => db.BackOfficeHandoffs.AsNoTracking().SingleAsync(h => h.UserId == id));

        Assert.Equal(ResetTokens.Hash(handoff.Code), stored.CodeHash);
        Assert.NotEqual(handoff.Code, stored.CodeHash);
        Assert.InRange(stored.ExpiresAt - before, TimeSpan.FromSeconds(29), TimeSpan.FromSeconds(31));
    }

    [Fact]
    public async Task Only_staff_with_two_factor_sign_in_are_given_one()
    {
        var customer = await NewUserAsync();
        var unenrolled = await NewUserAsync(RoleNames.Moderator);

        var notStaff = await Assert.ThrowsAsync<ForbiddenException>(() => SendAsync(customer, new IssueBackOfficeHandoffCommand()));
        var noCode = await Assert.ThrowsAsync<ForbiddenException>(() => SendAsync(unenrolled, new IssueBackOfficeHandoffCommand()));

        Assert.Equal("NotStaff", notStaff.Facts["code"]);
        Assert.Equal("TwoFactorSetupRequired", noCode.Facts["code"]);
        Assert.Equal(0, await ReadAsync(db => db.BackOfficeHandoffs.CountAsync(h => h.UserId == customer || h.UserId == unenrolled)));
    }

    /// <summary>The guarded claim: of ten redemptions at once, exactly one yields a challenge.</summary>
    [Fact]
    public async Task Ten_redemptions_at_once_yield_one_challenge()
    {
        var (id, _, _) = await EnrolledAsync(RoleNames.Admin);
        var handoff = await SendAsync(id, new IssueBackOfficeHandoffCommand());

        var attempts = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            try
            {
                await SendAsync(Guid.Empty, new RedeemBackOfficeHandoffCommand(handoff.Code));
                return true;
            }
            catch (ValidationException)
            {
                return false;
            }
        }));

        Assert.Equal(1, attempts.Count(won => won));
    }

    // ------------------------------------------------------------------ helpers

    private async Task<Guid> NewUserAsync(string? role = null)
    {
        var email = $"ho-{Guid.NewGuid():N}@example.test";
        var registered = await SendAsync(Guid.Empty, new RegisterCommand(email, Password, "Hand", "Off"));
        if (role is not null)
        {
            await DbAsync(async db =>
            {
                var user = await db.Users.Include(u => u.Roles).SingleAsync(u => u.Id == registered.Id);
                user.Roles.Add(await db.Roles.SingleAsync(r => r.Name == role));
                await db.SaveChangesAsync();
            });
        }

        return registered.Id;
    }

    private async Task<(Guid Id, string Email, string Secret)> EnrolledAsync(string role)
    {
        var id = await NewUserAsync(role);
        var setup = await SendAsync(id, new StartTwoFactorSetupCommand());
        await SendAsync(id, new ConfirmTwoFactorCommand(CodeFor(setup.Secret, -1)));
        await DbAsync(db => db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(x => x.SetProperty(u => u.TwoFactorLastStep, (long?)null)));
        var email = await ReadAsync(db => db.Users.Where(u => u.Id == id).Select(u => u.Email).SingleAsync());
        return (id, email, setup.Secret);
    }

    private static string CodeFor(string base32Secret, int stepsAway = 0) =>
        Totp.Code(Base32.Decode(base32Secret)!, Totp.StepAt(DateTime.UtcNow) + stepsAway);

    private static IEnumerable<string> TokenRoles(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token).Claims
            .Where(c => c.Type is "role" or System.Security.Claims.ClaimTypes.Role).Select(c => c.Value);

    private async Task DbAsync(Func<ApplicationDbContext, Task> work)
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        await work(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private async Task<T> ReadAsync<T>(Func<ApplicationDbContext, Task<T>> read)
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        return await read(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private async Task<T> SendAsync<T>(Guid caller, IRequest<T> request)
    {
        await using var provider = _fixture.For(caller);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
