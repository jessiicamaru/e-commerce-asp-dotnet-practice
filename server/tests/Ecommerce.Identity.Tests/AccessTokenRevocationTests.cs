using System.Security.Claims;
using Ecommerce.Application.Auth.Commands.Account;
using Ecommerce.Application.Auth.Commands.Login;
using Ecommerce.Application.Auth.Commands.PasswordReset;
using Ecommerce.Application.Auth.Commands.Refresh;
using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Application.Users;
using Ecommerce.Contracts.Identity;
using Ecommerce.Domain.Constants;
using Ecommerce.Infrastructure.Persistence;
using Ecommerce.Shared.Authentication;
using MassTransit;
using MassTransit.Testing;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// An access token issued before its user's access was revoked stops working within seconds (specs/065,
/// #112) - it used to live out its 15 minutes after a ban.
/// </summary>
public class AccessTokenRevocationTests
{
    private static readonly DateTime Noon = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_token_issued_before_the_revocation_is_refused_and_one_issued_after_is_not()
    {
        var revoked = new RevokedAccessTokens(new FixedClock(Noon));
        var lan = Guid.NewGuid();

        revoked.Revoke(lan, Noon.AddMilliseconds(500));

        Assert.True(revoked.IsRevoked(lan, Noon.AddSeconds(-1)));
        Assert.False(revoked.IsRevoked(lan, Noon.AddSeconds(1)));
        Assert.False(revoked.IsRevoked(Guid.NewGuid(), Noon.AddSeconds(-1)));
    }

    /// <summary>
    /// iat is in whole seconds: the new token of the session a password change keeps is issued in the same
    /// second as the revocation, and must not be refused - it would refresh for ever.
    /// </summary>
    [Fact]
    public void A_token_issued_in_the_same_second_is_accepted()
    {
        var revoked = new RevokedAccessTokens(new FixedClock(Noon));
        var lan = Guid.NewGuid();

        revoked.Revoke(lan, Noon.AddMilliseconds(800));

        Assert.False(revoked.IsRevoked(lan, Noon));
    }

    [Fact]
    public void The_latest_revocation_wins_whatever_order_they_arrive_in()
    {
        var revoked = new RevokedAccessTokens(new FixedClock(Noon));
        var lan = Guid.NewGuid();

        revoked.Revoke(lan, Noon);
        revoked.Revoke(lan, Noon.AddMinutes(-5));   // an older message, delivered late

        Assert.True(revoked.IsRevoked(lan, Noon.AddSeconds(-1)));
    }

    [Fact]
    public void A_revocation_is_forgotten_once_every_token_it_could_refuse_has_expired()
    {
        var clock = new FixedClock(Noon);
        var revoked = new RevokedAccessTokens(clock);
        var old = Guid.NewGuid();
        revoked.Revoke(old, Noon);

        clock.Now = Noon + RevokedAccessTokens.Retention + TimeSpan.FromMinutes(1);
        revoked.Revoke(Guid.NewGuid(), clock.Now);

        Assert.Equal(1, revoked.Count);
        Assert.False(revoked.IsRevoked(old, Noon.AddSeconds(-1)));
    }

    [Fact]
    public void A_validated_token_is_asked_by_its_sub_and_iat()
    {
        var revoked = new RevokedAccessTokens(new FixedClock(Noon));
        var lan = Guid.NewGuid();
        revoked.Revoke(lan, Noon);

        Assert.True(revoked.IsRevoked(Principal(lan, Noon.AddMinutes(-3))));
        Assert.False(revoked.IsRevoked(Principal(lan, Noon.AddSeconds(2))));
        Assert.False(revoked.IsRevoked(new ClaimsPrincipal(new ClaimsIdentity())));   // no sub: not ours to judge
    }

    /// <summary>The hook every service gets from AddJwtAuthentication: a revoked token fails, a later one passes.</summary>
    [Fact]
    public async Task Every_service_refuses_a_revoked_token_when_it_validates_it()
    {
        await using var provider = JwtServices();
        var revoked = provider.GetRequiredService<RevokedAccessTokens>();
        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
        var lan = Guid.NewGuid();
        revoked.Revoke(lan, DateTime.UtcNow);

        var before = await ValidatedAsync(provider, options, Principal(lan, DateTime.UtcNow.AddMinutes(-2)));
        var after = await ValidatedAsync(provider, options, Principal(lan, DateTime.UtcNow.AddSeconds(2)));

        Assert.NotNull(before.Result?.Failure);
        Assert.Null(after.Result?.Failure);
    }

    [Fact]
    public async Task The_consumer_records_what_Identity_published()
    {
        await using var provider = new ServiceCollection()
            .AddSingleton(TimeProvider.System)
            .AddSingleton<RevokedAccessTokens>()
            .AddLogging()
            .AddMassTransitTestHarness(x => x.AddConsumer<AccessTokensRevokedConsumer>())
            .BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        var lan = Guid.NewGuid();

        await harness.Bus.Publish(new AccessTokensRevoked(lan, DateTime.UtcNow, "Banned"));

        Assert.True(await harness.Consumed.Any<AccessTokensRevoked>());
        Assert.True(provider.GetRequiredService<RevokedAccessTokens>().IsRevoked(lan, DateTime.UtcNow.AddMinutes(-1)));
    }

    // ------------------------------------------------------------------ helpers

    private static ClaimsPrincipal Principal(Guid userId, DateTime issuedAt) =>
        new(new ClaimsIdentity([
            new Claim("sub", userId.ToString()),
            new Claim("iat", new DateTimeOffset(issuedAt).ToUnixTimeSeconds().ToString()),
        ], "Bearer"));

    private static ServiceProvider JwtServices()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:Secret"] = "identity-tests-signing-key-of-at-least-32-bytes",
            ["JwtSettings:Issuer"] = "EcommerceApi",
            ["JwtSettings:Audience"] = "EcommerceClient",
        }).Build();
        var services = new ServiceCollection().AddLogging();
        services.AddJwtAuthentication(configuration);
        return services.BuildServiceProvider();
    }

    private static async Task<TokenValidatedContext> ValidatedAsync(IServiceProvider provider, JwtBearerOptions options, ClaimsPrincipal principal)
    {
        var http = new DefaultHttpContext { RequestServices = provider };
        var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
        var context = new TokenValidatedContext(http, scheme, options) { Principal = principal };
        await options.Events.OnTokenValidated(context);
        return context;
    }

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public DateTime Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => new(Now);
    }
}

/// <summary>Each of the six ways Identity ends access publishes the revocation, through its outbox, with the change.</summary>
[Collection(nameof(IdentityTestCollection))]
public class AccessTokenRevocationPublishingTests(IdentityTestFixture fixture)
{
    private const string Password = "Passw0rd!23";
    private static readonly Guid Admin = Guid.CreateVersion7();
    private readonly IdentityTestFixture _fixture = fixture;

    [Fact]
    public async Task A_lock_revokes()
    {
        var (id, _) = await PersonAsync();
        Assert.Equal("Locked", await RevokedByAsync(id, Admin, new LockUserCommand(id, 3, "Spam"), RoleNames.Admin));
    }

    [Fact]
    public async Task A_ban_revokes()
    {
        var (id, _) = await PersonAsync();
        Assert.Equal("Banned", await RevokedByAsync(id, Admin, new BanUserCommand(id, "Fraud"), RoleNames.Admin));
    }

    [Fact]
    public async Task Revoking_a_role_revokes()
    {
        var (id, _) = await PersonAsync();
        await SendAsync(Admin, new GrantRoleCommand(id, RoleNames.Moderator), RoleNames.Admin);

        Assert.Equal("RoleRevoked", await RevokedByAsync(id, Admin, new RevokeRoleCommand(id, RoleNames.Moderator), RoleNames.Admin));
    }

    [Fact]
    public async Task Changing_the_password_revokes()
    {
        var (id, _) = await PersonAsync();
        Assert.Equal("PasswordChanged", await RevokedByAsync(id, id, new ChangePasswordCommand(Password, "N3w-Passw0rd!")));
    }

    [Fact]
    public async Task Resetting_the_password_revokes()
    {
        var (id, email) = await PersonAsync();
        await SendAsync(Guid.Empty, new ForgotPasswordCommand(email, "vi"));
        var token = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(await WithDbAsync(db =>
            db.OutgoingEmails.Where(e => e.RecipientId == id && e.Template == "PasswordReset").Select(e => e.DataJson).SingleAsync()))!["token"];

        Assert.Equal("PasswordReset", await RevokedByAsync(id, Guid.Empty, new ResetPasswordCommand(token, "N3w-Passw0rd!")));
    }

    [Fact]
    public async Task A_reused_refresh_token_revokes()
    {
        var (id, email) = await PersonAsync();
        var first = await SendAsync(Guid.Empty, new LoginCommand(email, Password));
        await SendAsync(Guid.Empty, new RefreshTokenCommand(first.RefreshToken));
        await _fixture.AgeRevocationAsync(first.RefreshToken);

        Assert.Equal("SessionReuseDetected", await RevokedByAsync(id, Guid.Empty, new RefreshTokenCommand(first.RefreshToken), expectFailure: true));
    }

    [Fact]
    public async Task Granting_a_role_revokes_nothing()
    {
        var (id, _) = await PersonAsync();
        Assert.Null(await RevokedByAsync(id, Admin, new GrantRoleCommand(id, RoleNames.Moderator), RoleNames.Admin));
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Sends the request and returns the reason of the revocation it published for <paramref name="userId"/>, if any.</summary>
    private async Task<string?> RevokedByAsync(Guid userId, Guid caller, IBaseRequest request, string role = RoleNames.Customer, bool expectFailure = false)
    {
        await using var provider = _fixture.For(caller, role);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        await using (var scope = provider.CreateAsyncScope())
        {
            try { await scope.ServiceProvider.GetRequiredService<ISender>().Send((object)request); }
            catch (Exception) when (expectFailure) { }
        }

        return harness.Published.Select<AccessTokensRevoked>().Select(x => x.Context.Message)
            .SingleOrDefault(m => m.UserId == userId)?.Reason;
    }

    private async Task<(Guid Id, string Email)> PersonAsync()
    {
        var email = $"revoke-{Guid.NewGuid():N}@example.test";
        var registered = await SendAsync(Guid.Empty, new RegisterCommand(email, Password, "Lan", "Pham"));
        return (registered.Id, email);
    }

    private async Task<T> WithDbAsync<T>(Func<ApplicationDbContext, Task<T>> work)
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private async Task SendAsync(Guid caller, IRequest request, params string[] roles)
    {
        await using var provider = _fixture.For(caller, roles);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task<T> SendAsync<T>(Guid caller, IRequest<T> request, params string[] roles)
    {
        await using var provider = _fixture.For(caller, roles);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
