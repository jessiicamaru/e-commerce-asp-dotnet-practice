using System.IdentityModel.Tokens.Jwt;
using Ecommerce.Application.Auth.Commands.Login;
using Ecommerce.Application.Auth.Commands.Refresh;
using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Application.Auth.Common;
using Ecommerce.Application.Auth.TwoFactor;
using Ecommerce.Contracts.Activity;
using Ecommerce.Contracts.Identity;
using Ecommerce.Domain.Constants;
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
/// Staff sign in with a second factor (#218, specs/110): setting up an authenticator, the two-step sign-in, a code
/// working once, the pause, recovery codes, turning it off and an administrator's reset - and staff roles written into a
/// token only for a session verified with a code, which is what makes every service refuse an unverified one.
/// </summary>
[Collection(nameof(IdentityTestCollection))]
public class TwoFactorTests(IdentityTestFixture fixture)
{
    private const string Password = "Passw0rd!23";
    private readonly IdentityTestFixture _fixture = fixture;

    // ------------------------------------------------------------------ US1: setting it up

    [Fact]
    public async Task Setting_it_up_takes_a_right_code_and_answers_ten_recovery_codes_once()
    {
        var (id, _) = await NewUserAsync();
        var setup = await SendAsync(id, new StartTwoFactorSetupCommand());

        Assert.Equal(32, setup.Secret.Length);   // 20 bytes of base32
        Assert.StartsWith("otpauth://totp/", setup.Uri);
        Assert.Contains($"secret={setup.Secret}", setup.Uri);

        var wrong = await Assert.ThrowsAsync<ValidationException>(() =>
            SendAsync(id, new ConfirmTwoFactorCommand(CodeFor(setup.Secret, stepsAway: -5))));
        Assert.Equal("Code", Assert.Single(wrong.Errors).PropertyName);
        Assert.False((await SendAsync(id, new GetMyTwoFactorQuery())).Enabled);

        var codes = await SendAsync(id, new ConfirmTwoFactorCommand(CodeFor(setup.Secret)));

        Assert.Equal(10, codes.Codes.Count);
        Assert.All(codes.Codes, c => Assert.Matches("^[A-Z2-7]{5}-[A-Z2-7]{5}$", c));
        var status = await SendAsync(id, new GetMyTwoFactorQuery());
        Assert.True(status.Enabled);
        Assert.Equal(10, status.RecoveryCodesLeft);
        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(id, new StartTwoFactorSetupCommand()));

        // The secret is kept sealed, never in the clear, and no recovery code is stored as typed.
        var (secretStored, hashes) = await StoredAsync(id);
        Assert.DoesNotContain(setup.Secret, secretStored);
        Assert.DoesNotContain(hashes, h => codes.Codes.Any(c => h.Contains(c.Replace("-", ""))));
    }

    [Fact]
    public async Task Staff_without_it_hold_no_staff_role_until_it_is_set_up_and_then_the_session_carries_it()
    {
        var (id, email) = await NewUserAsync(RoleNames.Moderator);

        var session = await SendAsync(Guid.Empty, new LoginCommand(email, Password));
        Assert.Equal(SessionRoles.SetupRequired, session.TwoFactor);
        Assert.DoesNotContain(RoleNames.Moderator, session.Roles);
        Assert.DoesNotContain(RoleNames.Moderator, TokenRoles(session.Token));
        var elsewhere = await SendAsync(Guid.Empty, new LoginCommand(email, Password));

        var setup = await SendAsync(id, new StartTwoFactorSetupCommand());
        await SendAsync(id, new ConfirmTwoFactorCommand(CodeFor(setup.Secret), session.RefreshToken));

        // The session that confirmed it is now verified: renewed, it carries the role - in the answer and the token.
        var renewed = await SendAsync(Guid.Empty, new RefreshTokenCommand(session.RefreshToken));
        Assert.Contains(RoleNames.Moderator, renewed.Roles);
        Assert.Contains(RoleNames.Moderator, TokenRoles(renewed.Token));
        Assert.Contains("otp", Amr(renewed.Token));
        Assert.Null(renewed.TwoFactor);

        // ...and it stays verified across the next rotation too.
        var again = await SendAsync(Guid.Empty, new RefreshTokenCommand(renewed.RefreshToken));
        Assert.Contains(RoleNames.Moderator, again.Roles);

        // Every other session - never verified - ended: revoked in the table, not only refused at its next refresh.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            SendAsync(Guid.Empty, new RefreshTokenCommand(elsewhere.RefreshToken)));
        Assert.NotNull(await RevokedAtAsync(elsewhere.RefreshToken));
    }

    [Fact]
    public async Task Two_sign_ins_racing_with_the_same_code_let_exactly_one_through()
    {
        var (_, email, secret, _) = await EnrolledAsync();

        // Several rounds: a race only shows when the two really overlap, and one round in which they do is enough.
        for (var round = 0; round < 5; round++)
        {
            await ForgetLastStepAsync(email);
            var code = CodeFor(secret);
            var one = await SendAsync(Guid.Empty, new LoginCommand(email, Password));
            var two = await SendAsync(Guid.Empty, new LoginCommand(email, Password));

            // Both read the same last-used window and both find the code right; only the guarded write can choose.
            var results = await Task.WhenAll(
                Record.ExceptionAsync(() => SendAsync(Guid.Empty, new LoginTwoFactorCommand(one.Challenge!, code, null))),
                Record.ExceptionAsync(() => SendAsync(Guid.Empty, new LoginTwoFactorCommand(two.Challenge!, code, null))));

            Assert.Single(results, e => e is null);
            Assert.Single(results, e => e is ValidationException);
        }
    }

    [Fact]
    public async Task One_challenge_exchanged_twice_at_once_makes_one_session()
    {
        var (_, email, _, codes) = await EnrolledAsync();

        // Five rounds, two fresh recovery codes each - all ten: one round in which the two overlap is enough.
        for (var round = 0; round < 5; round++)
        {
            var first = await SendAsync(Guid.Empty, new LoginCommand(email, Password));

            // Two different right answers to the same challenge, at the same moment.
            var results = await Task.WhenAll(
                Record.ExceptionAsync(() => SendAsync(Guid.Empty, new LoginTwoFactorCommand(first.Challenge!, null, codes[2 * round]))),
                Record.ExceptionAsync(() => SendAsync(Guid.Empty, new LoginTwoFactorCommand(first.Challenge!, null, codes[2 * round + 1]))));

            Assert.Single(results, e => e is null);
            var refused = Assert.IsType<ValidationException>(Assert.Single(results, e => e is not null));
            Assert.Equal("Challenge", Assert.Single(refused.Errors).PropertyName);
        }
    }

    [Fact]
    public async Task A_session_of_an_account_with_it_on_that_was_never_verified_is_refused_at_refresh()
    {
        var (id, email, _, _) = await EnrolledAsync();
        var stray = new RefreshToken
        {
            Id = Guid.CreateVersion7(), UserId = id, Token = Guid.NewGuid().ToString("N"), ExpiresAt = DateTime.UtcNow.AddDays(1),
        };
        await DbAsync(db => db.RefreshTokens.Add(stray));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            SendAsync(Guid.Empty, new RefreshTokenCommand(stray.Token)));
    }

    // ------------------------------------------------------------------ US2: signing in

    [Fact]
    public async Task Signing_in_takes_the_code_after_the_password_and_nothing_is_issued_before_it()
    {
        var (_, email, secret, _) = await EnrolledAsync(RoleNames.Moderator);

        var first = await SendAsync(Guid.Empty, new LoginCommand(email, Password));
        Assert.Equal(SessionRoles.Required, first.TwoFactor);
        Assert.Equal(("", ""), (first.Token, first.RefreshToken));
        Assert.Empty(first.Roles);
        Assert.False(string.IsNullOrEmpty(first.Challenge));

        var session = await SendAsync(Guid.Empty, new LoginTwoFactorCommand(first.Challenge!, CodeFor(secret), null));

        Assert.False(string.IsNullOrEmpty(session.Token));
        Assert.False(string.IsNullOrEmpty(session.RefreshToken));
        Assert.Contains(RoleNames.Moderator, session.Roles);
        Assert.Contains(RoleNames.Moderator, TokenRoles(session.Token));
    }

    [Fact]
    public async Task A_code_works_once_and_a_challenge_is_exchanged_once()
    {
        var (_, email, secret, _) = await EnrolledAsync();
        var code = CodeFor(secret);

        var first = await SendAsync(Guid.Empty, new LoginCommand(email, Password));
        await SendAsync(Guid.Empty, new LoginTwoFactorCommand(first.Challenge!, code, null));

        // The same code a moment later - seen over a shoulder - is refused.
        var second = await SendAsync(Guid.Empty, new LoginCommand(email, Password));
        var replay = await Assert.ThrowsAsync<ValidationException>(() =>
            SendAsync(Guid.Empty, new LoginTwoFactorCommand(second.Challenge!, code, null)));
        Assert.Equal("Code", Assert.Single(replay.Errors).PropertyName);

        // The used challenge cannot be exchanged again, even with a right code.
        await ForgetLastStepAsync(email);
        var reused = await Assert.ThrowsAsync<ValidationException>(() =>
            SendAsync(Guid.Empty, new LoginTwoFactorCommand(first.Challenge!, CodeFor(secret), null)));
        Assert.Equal("Challenge", Assert.Single(reused.Errors).PropertyName);
    }

    [Fact]
    public async Task Wrong_codes_count_toward_the_pause_and_a_challenge_dies_after_five()
    {
        var (_, email, secret, _) = await EnrolledAsync();
        var first = await SendAsync(Guid.Empty, new LoginCommand(email, Password));

        for (var i = 0; i < TwoFactorChallenge.MaxFailedAttempts; i++)
        {
            await Assert.ThrowsAsync<ValidationException>(() =>
                SendAsync(Guid.Empty, new LoginTwoFactorCommand(first.Challenge!, CodeFor(secret, stepsAway: -10 - i), null)));
        }

        var dead = await Assert.ThrowsAsync<ValidationException>(() =>
            SendAsync(Guid.Empty, new LoginTwoFactorCommand(first.Challenge!, CodeFor(secret), null)));
        Assert.Equal("Challenge", Assert.Single(dead.Errors).PropertyName);

        // Five wrong codes paused the email, as five wrong passwords would: even the right password waits.
        await Assert.ThrowsAsync<TooManyRequestsException>(() => SendAsync(Guid.Empty, new LoginCommand(email, Password)));
    }

    [Fact]
    public async Task The_right_password_does_not_restart_the_count_of_wrong_codes()
    {
        var (_, email, secret, _) = await EnrolledAsync();

        // Four wrong codes, each on a fresh challenge from the right password...
        for (var i = 0; i < 4; i++)
        {
            var challenge = (await SendAsync(Guid.Empty, new LoginCommand(email, Password))).Challenge!;
            await Assert.ThrowsAsync<ValidationException>(() =>
                SendAsync(Guid.Empty, new LoginTwoFactorCommand(challenge, CodeFor(secret, stepsAway: -10 - i), null)));
        }

        // ...and the fifth pauses the email: the passwords in between restarted nothing.
        var last = (await SendAsync(Guid.Empty, new LoginCommand(email, Password))).Challenge!;
        await Assert.ThrowsAsync<ValidationException>(() =>
            SendAsync(Guid.Empty, new LoginTwoFactorCommand(last, CodeFor(secret, stepsAway: -20), null)));
        await Assert.ThrowsAsync<TooManyRequestsException>(() => SendAsync(Guid.Empty, new LoginCommand(email, Password)));
    }

    [Fact]
    public async Task The_wrong_password_is_the_ordinary_refusal_and_says_nothing_about_two_factor()
    {
        var (_, email, _, _) = await EnrolledAsync();

        var refused = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            SendAsync(Guid.Empty, new LoginCommand(email, "Wrong-password1")));
        Assert.Equal("Invalid email or password.", refused.Message);
    }

    [Fact]
    public async Task An_expired_challenge_is_refused()
    {
        var (_, email, secret, _) = await EnrolledAsync();
        var first = await SendAsync(Guid.Empty, new LoginCommand(email, Password));
        await DbAsync(db => db.TwoFactorChallenges.Where(c => c.TokenHash == TwoFactorCodes.Hash(first.Challenge!))
            .ExecuteUpdateAsync(x => x.SetProperty(c => c.ExpiresAt, DateTime.UtcNow.AddSeconds(-1))));

        var expired = await Assert.ThrowsAsync<ValidationException>(() =>
            SendAsync(Guid.Empty, new LoginTwoFactorCommand(first.Challenge!, CodeFor(secret), null)));
        Assert.Equal("Challenge", Assert.Single(expired.Errors).PropertyName);
    }

    // ------------------------------------------------------------------ US3: a lost phone

    [Fact]
    public async Task A_recovery_code_signs_in_once_however_it_is_typed()
    {
        var (id, email, _, codes) = await EnrolledAsync();
        var typedLoosely = codes[0].ToLowerInvariant().Replace("-", " ");

        var first = await SendAsync(Guid.Empty, new LoginCommand(email, Password));
        var session = await SendAsync(Guid.Empty, new LoginTwoFactorCommand(first.Challenge!, null, typedLoosely));
        Assert.False(string.IsNullOrEmpty(session.Token));
        Assert.Equal(9, (await SendAsync(id, new GetMyTwoFactorQuery())).RecoveryCodesLeft);

        var second = await SendAsync(Guid.Empty, new LoginCommand(email, Password));
        var spent = await Assert.ThrowsAsync<ValidationException>(() =>
            SendAsync(Guid.Empty, new LoginTwoFactorCommand(second.Challenge!, null, codes[0])));
        Assert.Equal("RecoveryCode", Assert.Single(spent.Errors).PropertyName);
    }

    [Fact]
    public async Task New_recovery_codes_need_a_code_and_the_old_set_stops_working()
    {
        var (id, email, secret, old) = await EnrolledAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            SendAsync(id, new NewRecoveryCodesCommand(CodeFor(secret, stepsAway: -5))));
        var fresh = await SendAsync(id, new NewRecoveryCodesCommand(CodeFor(secret)));
        Assert.Equal(10, fresh.Codes.Count);

        var first = await SendAsync(Guid.Empty, new LoginCommand(email, Password));
        await Assert.ThrowsAsync<ValidationException>(() =>
            SendAsync(Guid.Empty, new LoginTwoFactorCommand(first.Challenge!, null, old[0])));
        await SendAsync(Guid.Empty, new LoginTwoFactorCommand(first.Challenge!, null, fresh.Codes[0]));
    }

    [Fact]
    public async Task An_administrator_resets_it_ending_every_session_and_telling_the_owner()
    {
        var (id, email, secret, _) = await EnrolledAsync(RoleNames.Moderator);
        var first = await SendAsync(Guid.Empty, new LoginCommand(email, Password));
        var session = await SendAsync(Guid.Empty, new LoginTwoFactorCommand(first.Challenge!, CodeFor(secret), null));

        var admin = Guid.CreateVersion7();
        var (_, published) = await PublishedAsync(admin, new ResetTwoFactorCommand(id), RoleNames.Admin);

        Assert.Contains(published.OfType<AccessTokensRevoked>(), r => r.UserId == id && r.Reason == "TwoFactorReset");
        Assert.Contains(published.OfType<EmailRequested>(), e => e.RecipientId == id && e.Template == "TwoFactorReset");
        Assert.Contains(published.OfType<AuditEntryRecorded>(), a => a.Action == "TwoFactorReset" && a.AboutUserId == id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            SendAsync(Guid.Empty, new RefreshTokenCommand(session.RefreshToken)));

        // Next time, setup again: a moderator without it.
        var next = await SendAsync(Guid.Empty, new LoginCommand(email, Password));
        Assert.Equal(SessionRoles.SetupRequired, next.TwoFactor);
    }

    [Fact]
    public async Task Only_an_administrator_resets_it_and_never_their_own()
    {
        var (id, _, _, _) = await EnrolledAsync(RoleNames.Admin);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            SendAsync(Guid.CreateVersion7(), new ResetTwoFactorCommand(id), RoleNames.Moderator));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            SendAsync(id, new ResetTwoFactorCommand(id), RoleNames.Admin));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            SendAsync(Guid.CreateVersion7(), new ResetTwoFactorCommand(Guid.CreateVersion7()), RoleNames.Admin));
    }

    // ------------------------------------------------------------------ US4: anybody may turn it on - and off, unless staff

    [Fact]
    public async Task A_customer_turns_it_off_with_the_password_and_a_code_staff_cannot()
    {
        var (customer, _, secret, _) = await EnrolledAsync();
        await Assert.ThrowsAsync<ValidationException>(() =>
            SendAsync(customer, new DisableTwoFactorCommand("Wrong-password1", CodeFor(secret))));

        await SendAsync(customer, new DisableTwoFactorCommand(Password, CodeFor(secret)));
        Assert.False((await SendAsync(customer, new GetMyTwoFactorQuery())).Enabled);

        var (moderator, _, modSecret, _) = await EnrolledAsync(RoleNames.Moderator);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            SendAsync(moderator, new DisableTwoFactorCommand(Password, CodeFor(modSecret))));
        Assert.True((await SendAsync(moderator, new GetMyTwoFactorQuery())).Required);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>The code an authenticator shows now - or <paramref name="stepsAway"/> windows off, outside the window.</summary>
    private static string CodeFor(string base32Secret, int stepsAway = 0) =>
        Totp.Code(Base32.Decode(base32Secret)!, Totp.StepAt(DateTime.UtcNow) + stepsAway);

    private async Task<(Guid Id, string Email)> NewUserAsync(params string[] roles)
    {
        var email = $"tfa-{Guid.NewGuid():N}@example.test";
        var registered = await SendAsync(Guid.Empty, new RegisterCommand(email, Password, "Two", "Factor"));
        if (roles.Length > 0)
        {
            await DbAsync(async db =>
            {
                var user = await db.Users.Include(u => u.Roles).SingleAsync(u => u.Id == registered.Id);
                foreach (var role in await db.Roles.Where(r => roles.Contains(r.Name)).ToListAsync())
                    user.Roles.Add(role);
                await db.SaveChangesAsync();
            });
        }

        return (registered.Id, email);
    }

    /// <summary>
    /// An account with 2FA on - its secret and recovery codes - and its last used window forgotten, so the test's
    /// first code is not refused as the one that confirmed setup.
    /// </summary>
    private async Task<(Guid Id, string Email, string Secret, IReadOnlyList<string> Codes)> EnrolledAsync(params string[] roles)
    {
        var (id, email) = await NewUserAsync(roles);
        var setup = await SendAsync(id, new StartTwoFactorSetupCommand());
        var codes = await SendAsync(id, new ConfirmTwoFactorCommand(CodeFor(setup.Secret)));
        await ForgetLastStepAsync(email);
        return (id, email, setup.Secret, codes.Codes);
    }

    private Task ForgetLastStepAsync(string email) =>
        DbAsync(db => db.Users.Where(u => u.Email == email).ExecuteUpdateAsync(x => x.SetProperty(u => u.TwoFactorLastStep, (long?)null)));

    private async Task<(string Secret, List<string> Hashes)> StoredAsync(Guid id)
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var secret = await db.Users.Where(u => u.Id == id).Select(u => u.TwoFactorSecret!).SingleAsync();
        var hashes = await db.TwoFactorRecoveryCodes.Where(c => c.UserId == id).Select(c => c.CodeHash).ToListAsync();
        return (secret, hashes);
    }

    private async Task<DateTime?> RevokedAtAsync(string refreshToken)
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().RefreshTokens
            .Where(t => t.Token == refreshToken).Select(t => t.RevokedAt).SingleAsync();
    }

    private static IEnumerable<string> TokenRoles(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token).Claims
            .Where(c => c.Type is "role" or System.Security.Claims.ClaimTypes.Role).Select(c => c.Value);

    private static IEnumerable<string> Amr(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.Where(c => c.Type == "amr").Select(c => c.Value);

    private async Task DbAsync(Func<ApplicationDbContext, Task> work)
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await work(db);
        await db.SaveChangesAsync();
    }

    private Task DbAsync(Action<ApplicationDbContext> work) => DbAsync(db => { work(db); return Task.CompletedTask; });

    private async Task<T> SendAsync<T>(Guid caller, IRequest<T> request, params string[] roles)
    {
        await using var provider = _fixture.For(caller, roles);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task SendAsync(Guid caller, IRequest request, params string[] roles)
    {
        await using var provider = _fixture.For(caller, roles);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    /// <summary>What one request published - through the outbox, so read from the harness after it committed.</summary>
    private async Task<(bool Done, List<object> Published)> PublishedAsync(Guid caller, IRequest request, params string[] roles)
    {
        await using var provider = _fixture.For(caller, roles);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
        var published = harness.Published.Select(_ => true).Select(m => m.MessageObject).ToList();
        return (true, published);
    }
}
