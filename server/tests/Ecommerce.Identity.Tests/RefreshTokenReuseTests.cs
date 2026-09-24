using Ecommerce.Application.Auth.Commands.Login;
using Ecommerce.Application.Auth.Commands.Refresh;
using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Application.Auth.Common;
using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Domain.Entities;
using Ecommerce.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Ecommerce.Application.Users;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// A stolen refresh token that comes back after its owner has refreshed is recognised, and ends every
/// session of that user (issue #29). Before this, rotation deleted the old token, and a replay was
/// indistinguishable from a typo.
/// </summary>
[Collection(nameof(IdentityTestCollection))]
public class RefreshTokenReuseTests(IdentityTestFixture fixture)
{
    private readonly IdentityTestFixture _fixture = fixture;

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task<T> WithDbAsync<T>(Func<ApplicationDbContext, Task<T>> work)
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private async Task<AuthResponse> SignedInAsync()
    {
        var email = $"reuse-{Guid.NewGuid():N}@example.test";
        await SendAsync(new RegisterCommand(email, "Passw0rd!23", "A", "B"));
        return await SendAsync(new LoginCommand(email, "Passw0rd!23"));
    }

    private Task<RefreshToken> RowAsync(string token) =>
        WithDbAsync(db => db.RefreshTokens.AsNoTracking().SingleAsync(t => t.Token == token));

    /// <summary>Moves a rotation into the past, beyond the grace window, as a later replay would find it.</summary>
    private Task AgeRevocationAsync(string token) =>
        WithDbAsync(db => db.RefreshTokens.Where(t => t.Token == token)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow.AddMinutes(-5))));

    /// <summary>
    /// #128 (specs/058): a token revoked by a LOCK was never rotated, so presenting it again is a stale tab,
    /// not theft. Treated as reuse, it ended the session the person signed in with after the unlock.
    /// </summary>
    [Fact]
    public async Task A_stale_tab_from_before_a_lock_does_not_end_the_session_after_the_unlock()
    {
        var before = await SignedInAsync();
        var admin = Guid.CreateVersion7();
        await AsAdminAsync(admin, new LockUserCommand(before.Id, 1, "Cooling off"));
        await AsAdminAsync(admin, new UnlockUserCommand(before.Id));
        var after = await SendAsync(new LoginCommand(before.Email, "Passw0rd!23"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => SendAsync(new RefreshTokenCommand(before.RefreshToken)));

        await SendAsync(new RefreshTokenCommand(after.RefreshToken));   // still a session
    }

    private async Task AsAdminAsync<T>(Guid admin, IRequest<T> request)
    {
        await using var provider = _fixture.For(admin, "Admin");
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    [Fact]
    public async Task Rotation_keeps_the_old_token_revoked_and_pointing_at_its_successor()
    {
        var a = await SignedInAsync();

        var b = await SendAsync(new RefreshTokenCommand(a.RefreshToken));

        var row = await RowAsync(a.RefreshToken);
        Assert.NotNull(row.RevokedAt);
        Assert.Equal(b.RefreshToken, row.ReplacedByToken);
    }

    [Fact]
    public async Task A_replayed_token_is_refused_and_ends_the_session_it_was_replaced_by()
    {
        var a = await SignedInAsync();
        var b = await SendAsync(new RefreshTokenCommand(a.RefreshToken));
        await AgeRevocationAsync(a.RefreshToken);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => SendAsync(new RefreshTokenCommand(a.RefreshToken)));

        // B was the owner's - or the thief's. The server cannot tell, so it no longer opens anything.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => SendAsync(new RefreshTokenCommand(b.RefreshToken)));
        Assert.NotNull((await RowAsync(b.RefreshToken)).RevokedAt);
    }

    [Fact]
    public async Task Reuse_ends_every_session_of_that_user_and_nobody_elses()
    {
        var phone = await SignedInAsync();
        var laptop = await SendAsync(new LoginCommand(phone.Email, "Passw0rd!23"));
        var stranger = await SignedInAsync();

        var rotated = await SendAsync(new RefreshTokenCommand(phone.RefreshToken));
        await AgeRevocationAsync(phone.RefreshToken);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => SendAsync(new RefreshTokenCommand(phone.RefreshToken)));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => SendAsync(new RefreshTokenCommand(rotated.RefreshToken)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => SendAsync(new RefreshTokenCommand(laptop.RefreshToken)));
        await SendAsync(new RefreshTokenCommand(stranger.RefreshToken));   // untouched
    }

    [Fact]
    public async Task Within_the_grace_window_a_second_tab_is_refused_without_ending_the_session()
    {
        var a = await SignedInAsync();
        var b = await SendAsync(new RefreshTokenCommand(a.RefreshToken));

        // The other tab, a moment later, still holding A.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => SendAsync(new RefreshTokenCommand(a.RefreshToken)));

        var c = await SendAsync(new RefreshTokenCommand(b.RefreshToken));
        Assert.False(string.IsNullOrWhiteSpace(c.RefreshToken));
    }

    [Fact]
    public async Task Simultaneous_refreshes_with_one_token_mint_exactly_one_successor()
    {
        var a = await SignedInAsync();
        // Registering signed in too, so this user already has a second, unrelated session.
        var activeBefore = await WithDbAsync(db => db.RefreshTokens.CountAsync(t => t.UserId == a.Id && t.RevokedAt == null));

        var attempts = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            try { return await SendAsync(new RefreshTokenCommand(a.RefreshToken)); }
            catch (UnauthorizedAccessException) { return null; }
        }));

        var winner = Assert.Single(attempts, r => r is not null);

        // A was replaced by exactly one token, not by one per request.
        var activeAfter = await WithDbAsync(db => db.RefreshTokens.CountAsync(t => t.UserId == a.Id && t.RevokedAt == null));
        Assert.Equal(activeBefore, activeAfter);
        Assert.Equal(winner!.RefreshToken, (await RowAsync(a.RefreshToken)).ReplacedByToken);
    }

    [Fact]
    public async Task The_rotation_itself_lets_only_one_of_several_simultaneous_callers_through()
    {
        // The handler test above cannot prove this alone: its own read already refuses a token that is
        // visibly revoked, so only callers whose reads overlap reach the write, and building a provider
        // per request makes that rare. Here every caller is ready before any starts, and each goes
        // straight to the write - the one statement that has to decide.
        var a = await SignedInAsync();
        var providers = Enumerable.Range(0, 10).Select(_ => _fixture.For(Guid.Empty)).ToList();
        var scopes = providers.Select(p => p.CreateAsyncScope()).ToList();

        try
        {
            var now = DateTime.UtcNow;
            var results = await Task.WhenAll(scopes.Select(scope =>
                scope.ServiceProvider.GetRequiredService<IUserRepository>().TryRotateRefreshTokenAsync(
                    a.RefreshToken,
                    new RefreshToken { Token = $"successor-{Guid.NewGuid():N}", UserId = a.Id, ExpiresAt = now.AddDays(7) },
                    now)));

            Assert.Equal(1, results.Count(won => won));
        }
        finally
        {
            foreach (var scope in scopes) await scope.DisposeAsync();
            foreach (var provider in providers) await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task Rotation_clears_the_users_expired_tokens()
    {
        var a = await SignedInAsync();
        var expired = new RefreshToken
        {
            Token = $"expired-{Guid.NewGuid():N}",
            UserId = a.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        };
        await WithDbAsync(async db => { db.RefreshTokens.Add(expired); return await db.SaveChangesAsync(); });

        await SendAsync(new RefreshTokenCommand(a.RefreshToken));

        Assert.False(await WithDbAsync(db => db.RefreshTokens.AnyAsync(t => t.Token == expired.Token)));
        // The just-revoked token is not expired, so it stays: it is what recognises a replay.
        Assert.True(await WithDbAsync(db => db.RefreshTokens.AnyAsync(t => t.Token == a.RefreshToken)));
    }
}
