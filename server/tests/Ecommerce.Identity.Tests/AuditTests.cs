using Ecommerce.Application.Addresses.Commands.SaveAddress;
using Ecommerce.Application.Addresses.Commands.SetDefaultAddress;
using Ecommerce.Application.Auth.Commands.Login;
using Ecommerce.Application.Auth.Commands.Logout;
using Ecommerce.Application.Auth.Commands.Refresh;
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

        // Registering also sends the confirmation link (specs/063), recorded as its own entry.
        Assert.Equal(["Registered", "EmailConfirmationSent"], registered.Select(e => e.Action));
        var reg = registered[0];
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
            new RegisterSellerCommand(email, "Passw0rd!23", "Mai", "Tran", "Mai Lens")), e => e.Action != "EmailConfirmationSent");
        Assert.Equal("ShopApplied", applied.Action);

        // The shop exists once a moderator approves it (specs/044); then it can be renamed.
        var seller = await _fixture.ApprovedSellerAsync($"audit-{Guid.NewGuid():N}@example.test", "Mai Lens");
        var renamed = Assert.Single(await RecordedAsync(seller.Id, new RenameShopCommand("Mai Lens Ha Noi")));

        Assert.Equal("ShopRenamed", renamed.Action);
        Assert.Contains("Mai Lens", renamed.Before);
        Assert.Contains("Mai Lens Ha Noi", renamed.After);
    }

    /// <summary>#128 (specs/058): signing out was not on the record; an unknown token records nothing.</summary>
    [Fact]
    public async Task Signing_out_is_recorded_as_the_person_and_nothing_is_recorded_for_nothing()
    {
        var email = $"audit-{Guid.NewGuid():N}@example.test";
        await RecordedAsync(Guid.Empty, new RegisterCommand(email, "Passw0rd!23", "Lan", "Pham"));
        var session = await SendAsync(new LoginCommand(email, "Passw0rd!23"));

        var signedOut = Assert.Single(await RecordedAsync(Guid.Empty, new LogoutCommand(session.RefreshToken)));
        Assert.Equal(("Security", "SignedOut", (Guid?)session.Id), (signedOut.Category, signedOut.Action, signedOut.ActorId));
        Assert.DoesNotContain(session.RefreshToken, signedOut.Summary + signedOut.After);

        Assert.Empty(await RecordedAsync(Guid.Empty, new LogoutCommand("not-a-token")));
    }

    /// <summary>#128: the default address is on the record - as "changed", never as an address.</summary>
    [Fact]
    public async Task Changing_the_default_address_is_recorded_without_the_address()
    {
        var email = $"audit-{Guid.NewGuid():N}@example.test";
        await RecordedAsync(Guid.Empty, new RegisterCommand(email, "Passw0rd!23", "Lan", "Pham"));
        var me = (await SendAsync(new LoginCommand(email, "Passw0rd!23"))).Id;
        await SendAsync(new SaveAddressCommand("Lan Pham", "12 Ly Thuong Kiet", null, "Ha Noi", null, "100000", "VN", null), me);
        var second = await SendAsync(new SaveAddressCommand("Lan at work", "1 Trang Tien", null, "Ha Noi", null, "100000", "VN", null), me);

        var entry = Assert.Single(await RecordedAsync(me, new SetDefaultAddressCommand(second.Id)));

        Assert.Equal(("User", "DefaultAddressChanged"), (entry.Category, entry.Action));
        Assert.DoesNotContain("Trang Tien", entry.Summary + entry.Before + entry.After);
    }

    /// <summary>#128: reuse is a security event worth keeping, not just a log line.</summary>
    [Fact]
    public async Task Detected_reuse_is_on_the_record()
    {
        var email = $"audit-{Guid.NewGuid():N}@example.test";
        await RecordedAsync(Guid.Empty, new RegisterCommand(email, "Passw0rd!23", "Lan", "Pham"));
        var first = await SendAsync(new LoginCommand(email, "Passw0rd!23"));
        await SendAsync(new RefreshTokenCommand(first.RefreshToken));
        await _fixture.AgeRevocationAsync(first.RefreshToken);

        var entry = Assert.Single(await RecordedAsync(Guid.Empty, new RefreshTokenCommand(first.RefreshToken), expectFailure: true));

        Assert.Equal(("Security", "SessionReuseDetected", (Guid?)first.Id), (entry.Category, entry.Action, entry.ActorId));
    }

    private async Task<T> SendAsync<T>(IRequest<T> request, Guid? caller = null)
    {
        await using var provider = _fixture.For(caller ?? Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private Task<List<AuditEntryRecorded>> RecordedAsync(Guid caller, IRequest request, bool expectFailure = false) =>
        RecordedObjectAsync(caller, request, expectFailure);

    private Task<List<AuditEntryRecorded>> RecordedAsync<T>(Guid caller, IRequest<T> request, bool expectFailure = false) =>
        RecordedObjectAsync(caller, request, expectFailure);

    private async Task<List<AuditEntryRecorded>> RecordedObjectAsync(Guid caller, object request, bool expectFailure)
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
