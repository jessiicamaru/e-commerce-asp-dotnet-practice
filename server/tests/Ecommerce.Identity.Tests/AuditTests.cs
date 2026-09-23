using Ecommerce.Application.Auth.Commands.Login;
using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Application.Auth.Commands.RegisterSeller;
using Ecommerce.Application.Sellers;
using Ecommerce.Contracts.Activity;
using MassTransit.Testing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// What Identity tells the audit log (specs/041): sign-ins, refused sign-ins, registrations and shop
/// renames - one entry each, with who, and never a password.
/// </summary>
[Collection(nameof(IdentityTestCollection))]
public class AuditTests(IdentityTestFixture fixture)
{
    private readonly IdentityTestFixture _fixture = fixture;

    [Fact]
    public async Task Registering_and_signing_in_are_recorded_with_who_did_it()
    {
        var email = $"audit-{Guid.NewGuid():N}@example.test";

        var registered = await RecordedAsync(Guid.Empty, new RegisterCommand(email, "Passw0rd!23", "Lan", "Pham"));
        var signedIn = await RecordedAsync(Guid.Empty, new LoginCommand(email, "Passw0rd!23"));

        var reg = Assert.Single(registered);
        Assert.Equal(("User", "Registered", "Customer"), (reg.Category, reg.Action, reg.ActorRole));
        Assert.Equal(email, reg.ActorEmail);
        Assert.DoesNotContain("Passw0rd", reg.After);

        var login = Assert.Single(signedIn);
        Assert.Equal(("Security", "SignedIn"), (login.Category, login.Action));
        Assert.Equal(reg.ActorId, login.ActorId);
    }

    /// <summary>A refused sign-in is recorded even though the request fails - that is its whole point.</summary>
    [Fact]
    public async Task A_refused_sign_in_is_recorded()
    {
        var email = $"audit-{Guid.NewGuid():N}@example.test";
        await RecordedAsync(Guid.Empty, new RegisterCommand(email, "Passw0rd!23", "Lan", "Pham"));

        var refused = await RecordedAsync(Guid.Empty, new LoginCommand(email, "wrong-password"), expectFailure: true);
        var nobody = await RecordedAsync(Guid.Empty, new LoginCommand($"nobody-{Guid.NewGuid():N}@example.test", "x"), expectFailure: true);

        var entry = Assert.Single(refused);
        Assert.Equal(("Security", "SignInRefused"), (entry.Category, entry.Action));
        Assert.NotNull(entry.ActorId);
        Assert.DoesNotContain("wrong-password", entry.Summary);
        Assert.Null(Assert.Single(nobody).ActorId);
    }

    [Fact]
    public async Task Renaming_a_shop_records_the_old_and_the_new_name()
    {
        var email = $"audit-{Guid.NewGuid():N}@example.test";
        var applied = Assert.Single(await RecordedAsync(Guid.Empty,
            new RegisterSellerCommand(email, "Passw0rd!23", "Mai", "Tran", "Mai Lens")));
        Assert.Equal("ShopApplied", applied.Action);

        // The shop exists once a moderator approves it (specs/044); then it can be renamed.
        var seller = await _fixture.ApprovedSellerAsync($"audit-{Guid.NewGuid():N}@example.test", "Mai Lens");
        var renamed = Assert.Single(await RecordedAsync(seller.Id, new RenameShopCommand("Mai Lens Ha Noi")));

        Assert.Equal("ShopRenamed", renamed.Action);
        Assert.Contains("Mai Lens", renamed.Before);
        Assert.Contains("Mai Lens Ha Noi", renamed.After);
    }

    private async Task<List<AuditEntryRecorded>> RecordedAsync<T>(Guid caller, IRequest<T> request, bool expectFailure = false)
    {
        await using var provider = _fixture.For(caller);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await using (var scope = provider.CreateAsyncScope())
        {
            var send = scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
            if (expectFailure)
            {
                await Assert.ThrowsAnyAsync<Exception>(() => send);
            }
            else
            {
                await send;
            }
        }

        return harness.Published.Select<AuditEntryRecorded>().Select(x => x.Context.Message).ToList();
    }
}
