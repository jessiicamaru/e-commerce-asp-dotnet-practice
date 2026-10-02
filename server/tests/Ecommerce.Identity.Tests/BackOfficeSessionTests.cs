using System.IdentityModel.Tokens.Jwt;
using Ecommerce.Application.Auth.Commands.Login;
using Ecommerce.Application.Auth.Commands.Refresh;
using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Application.Auth.Common;
using Ecommerce.Application.Auth.TwoFactor;
using Ecommerce.Domain.Constants;
using Ecommerce.Domain.Entities;
using Ecommerce.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// Staff roles only in a back-office session (#278, specs/138, ADR-003): a staff member's session on the storefront -
/// where the public's words are shown - never carries Admin or Moderator, even verified with a code; the same person's
/// back-office session does. The app is the sign-in's Origin, stored with the session and carried across rotation.
/// </summary>
[Collection(nameof(IdentityTestCollection))]
public class BackOfficeSessionTests(IdentityTestFixture fixture)
{
    private const string Password = "Passw0rd!23";
    private readonly IdentityTestFixture _fixture = fixture;

    [Fact]
    public async Task An_administrator_verified_on_the_storefront_holds_no_staff_role_and_in_the_back_office_does()
    {
        var (_, email, secret) = await EnrolledAsync(RoleNames.Admin);

        var storefront = await SignInWithCodeAsync(email, secret, SessionClient.Storefront, stepsAway: 0);
        Assert.DoesNotContain(RoleNames.Admin, storefront.Roles);
        Assert.DoesNotContain(RoleNames.Admin, TokenRoles(storefront.Token));
        Assert.Contains(RoleNames.Customer, TokenRoles(storefront.Token));
        // The storefront still knows to offer the back office - to draw, never to decide.
        Assert.True(storefront.StaffAccount);

        var backOffice = await SignInWithCodeAsync(email, secret, SessionClient.BackOffice, stepsAway: 1);
        Assert.Contains(RoleNames.Admin, backOffice.Roles);
        Assert.Contains(RoleNames.Admin, TokenRoles(backOffice.Token));
        Assert.True(backOffice.StaffAccount);

        Assert.Equal(SessionClient.Storefront, await StoredClientAsync(storefront.RefreshToken));
        Assert.Equal(SessionClient.BackOffice, await StoredClientAsync(backOffice.RefreshToken));
    }

    /// <summary>A session keeps the app it was made for: refreshing a storefront session never gains a staff role.</summary>
    [Fact]
    public async Task A_refresh_keeps_the_app_the_session_was_made_for()
    {
        var (_, email, secret) = await EnrolledAsync(RoleNames.Moderator);
        var storefront = await SignInWithCodeAsync(email, secret, SessionClient.Storefront, stepsAway: 0);
        var backOffice = await SignInWithCodeAsync(email, secret, SessionClient.BackOffice, stepsAway: 1);

        // Asked from the other app each time: the stored client decides, not the refresh's Origin.
        var renewedStorefront = await WithClientAsync(SessionClient.BackOffice, () => SendAsync(new RefreshTokenCommand(storefront.RefreshToken)));
        var renewedBackOffice = await WithClientAsync(SessionClient.Storefront, () => SendAsync(new RefreshTokenCommand(backOffice.RefreshToken)));

        Assert.DoesNotContain(RoleNames.Moderator, TokenRoles(renewedStorefront.Token));
        Assert.Contains(RoleNames.Moderator, TokenRoles(renewedBackOffice.Token));
        Assert.Equal(SessionClient.Storefront, await StoredClientAsync(renewedStorefront.RefreshToken));
        Assert.Equal(SessionClient.BackOffice, await StoredClientAsync(renewedBackOffice.RefreshToken));
    }

    /// <summary>Every session from before specs/138 has no client, and is read as the storefront's - the less privileged.</summary>
    [Fact]
    public async Task A_session_from_before_is_a_storefront_session()
    {
        var (_, email, secret) = await EnrolledAsync(RoleNames.Admin);
        var session = await SignInWithCodeAsync(email, secret, SessionClient.BackOffice, stepsAway: 0);
        await DbAsync(db => db.RefreshTokens.Where(t => t.Token == session.RefreshToken)
            .ExecuteUpdateAsync(x => x.SetProperty(t => t.Client, (SessionClient?)null)));

        var renewed = await SendAsync(new RefreshTokenCommand(session.RefreshToken));

        Assert.DoesNotContain(RoleNames.Admin, TokenRoles(renewed.Token));
        Assert.True(renewed.StaffAccount);
    }

    /// <summary>A customer is a customer in either app; the flag says nothing about them.</summary>
    [Fact]
    public async Task A_customer_signing_in_is_told_they_are_not_staff()
    {
        var email = $"bo-{Guid.NewGuid():N}@example.test";
        await SendAsync(new RegisterCommand(email, Password, "Bo", "Customer"));

        var session = await WithClientAsync(SessionClient.BackOffice, () => SendAsync(new LoginCommand(email, Password)));

        Assert.Equal([RoleNames.Customer], session.Roles);
        Assert.False(session.StaffAccount);
    }

    // ------------------------------------------------------------------ the Origin rule

    [Theory]
    [InlineData("http://portal.localhost:5174", true)]
    [InlineData("http://PORTAL.localhost:5174/", true)]           // case and a trailing slash
    [InlineData("https://portal.example", true)]                  // the default port, written or not
    [InlineData("https://portal.example:443", true)]
    [InlineData("http://portal.example", false)]                  // another scheme is another origin
    [InlineData("http://portal.localhost:5175", false)]           // another port too
    [InlineData("http://localhost:5173", false)]                  // the storefront
    [InlineData("null", false)]                                   // an opaque origin
    [InlineData("", false)]
    [InlineData(null, false)]
    public void An_origin_is_the_back_office_only_when_it_is_one_of_its_own(string? origin, bool backOffice)
    {
        var configured = SessionClients.Parse(" http://portal.localhost:5174 , https://portal.example ");

        Assert.Equal(backOffice ? SessionClient.BackOffice : SessionClient.Storefront, SessionClients.FromOrigin(origin, configured));
    }

    [Fact]
    public void No_configured_origin_means_every_session_is_the_storefront()
    {
        Assert.Equal(SessionClient.Storefront, SessionClients.FromOrigin("http://portal.localhost:5174", SessionClients.Parse(null)));
    }

    // ------------------------------------------------------------------ helpers

    private async Task<AuthResponse> SignInWithCodeAsync(string email, string secret, SessionClient app, int stepsAway) =>
        await WithClientAsync(app, async () =>
        {
            var first = await SendAsync(new LoginCommand(email, Password));
            return await SendAsync(new LoginTwoFactorCommand(first.Challenge!, CodeFor(secret, stepsAway), null));
        });

    private async Task<T> WithClientAsync<T>(SessionClient app, Func<Task<T>> work)
    {
        var before = _fixture.SessionClient.Current;
        _fixture.SessionClient.Current = app;
        try
        {
            return await work();
        }
        finally
        {
            _fixture.SessionClient.Current = before;
        }
    }

    private async Task<(Guid Id, string Email, string Secret)> EnrolledAsync(string role)
    {
        var email = $"bo-{Guid.NewGuid():N}@example.test";
        var registered = await SendAsync(new RegisterCommand(email, Password, "Back", "Office"));
        await DbAsync(async db =>
        {
            var user = await db.Users.Include(u => u.Roles).SingleAsync(u => u.Id == registered.Id);
            user.Roles.Add(await db.Roles.SingleAsync(r => r.Name == role));
            await db.SaveChangesAsync();
        });
        var setup = await SendAsAsync(registered.Id, new StartTwoFactorSetupCommand());
        await SendAsAsync(registered.Id, new ConfirmTwoFactorCommand(CodeFor(setup.Secret, -1)));
        await DbAsync(db => db.Users.Where(u => u.Id == registered.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(u => u.TwoFactorLastStep, (long?)null)));
        return (registered.Id, email, setup.Secret);
    }

    private async Task<SessionClient?> StoredClientAsync(string refreshToken)
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().RefreshTokens
            .Where(t => t.Token == refreshToken).Select(t => t.Client).SingleAsync();
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

    private Task<T> SendAsync<T>(IRequest<T> request) => SendAsAsync(Guid.Empty, request);

    private async Task<T> SendAsAsync<T>(Guid caller, IRequest<T> request)
    {
        await using var provider = _fixture.For(caller);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
