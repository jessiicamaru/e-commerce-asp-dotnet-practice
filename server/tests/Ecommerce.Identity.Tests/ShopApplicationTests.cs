using Ecommerce.Application.Auth.Commands.Login;
using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Application.Auth.Commands.RegisterSeller;
using Ecommerce.Application.ShopApplications;
using Ecommerce.Contracts.Activity;
using Ecommerce.Contracts.Identity;
using Ecommerce.Domain.Constants;
using Ecommerce.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using MassTransit.Testing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// Opening a shop waits for somebody to look (specs/044): nobody sells until a moderator or an
/// administrator approves, and the approval does everything a registration used to - once.
/// </summary>
[Collection(nameof(IdentityTestCollection))]
public class ShopApplicationTests(IdentityTestFixture fixture)
{
    private const string Password = "Passw0rd!23";
    private readonly IdentityTestFixture _fixture = fixture;
    private static readonly Guid Moderator = Guid.CreateVersion7();

    [Fact]
    public async Task Registering_to_sell_opens_no_shop_and_tells_Catalog_nothing()
    {
        var (published, registered) = await PublishedAsync(Guid.Empty,
            new RegisterSellerCommand(AnEmail(), Password, "Mai", "Tran", "Mai Lens", "Mirrorless cameras", "0912 345 678"));

        Assert.Equal(["Customer"], registered.Roles);
        Assert.Empty(published.OfType<SellerRegisteredEvent>());
        var mine = Assert.Single(await SendAsync(registered.Id, new GetMyShopApplicationsQuery()));
        Assert.Equal(("Pending", "Mai Lens", "Mirrorless cameras"), (mine.Status, mine.ShopName, mine.Description));
        Assert.False(await HasShopAsync(registered.Id));
    }

    [Fact]
    public async Task Approval_makes_a_seller_opens_the_shop_and_tells_Catalog_and_the_applicant_once()
    {
        var registered = await SendAsync(Guid.Empty, new RegisterSellerCommand(AnEmail(), Password, "Mai", "Tran", "Mai Lens"));
        var id = (await SendAsync(registered.Id, new GetMyShopApplicationsQuery())).Single().Id;

        var (published, approved) = await PublishedAsync(Moderator, new ApproveShopApplicationCommand(id), RoleNames.Moderator);

        Assert.Equal("Approved", approved.Status);
        Assert.Equal((registered.Id, "Mai Lens"), (Assert.Single(published.OfType<SellerRegisteredEvent>()).SellerId, "Mai Lens"));
        var notice = Assert.Single(published.OfType<UserNotificationRequested>());
        Assert.Equal((registered.Id, "ShopApproved"), (notice.RecipientId, notice.Kind));
        var entry = Assert.Single(published.OfType<AuditEntryRecorded>());
        Assert.Equal(("Moderation", "ShopApproved"), (entry.Category, entry.Action));
        Assert.True(await HasShopAsync(registered.Id));

        var login = await SendAsync(Guid.Empty, new LoginCommand(registered.Email, Password));
        Assert.Contains(RoleNames.Seller, login.Roles);

        // A second decision is refused, and does none of it again.
        var (again, _) = await PublishedAsync(Moderator, new ApproveShopApplicationCommand(id), RoleNames.Moderator, expectFailure: true);
        Assert.Empty(again.OfType<SellerRegisteredEvent>());
    }

    /// <summary>Two moderators pressing Approve at once: one shop, one announcement, one 409.</summary>
    [Fact]
    public async Task Two_simultaneous_approvals_open_one_shop()
    {
        var registered = await SendAsync(Guid.Empty, new RegisterSellerCommand(AnEmail(), Password, "Mai", "Tran", "Mai Lens"));
        var id = (await SendAsync(registered.Id, new GetMyShopApplicationsQuery())).Single().Id;

        var attempts = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Attempt(() =>
            SendAsync(Moderator, new ApproveShopApplicationCommand(id), RoleNames.Moderator))));

        Assert.Equal(1, attempts.Count(ok => ok));
        Assert.True(await HasShopAsync(registered.Id));
    }

    [Fact]
    public async Task A_rejection_says_why_and_the_applicant_may_apply_again()
    {
        var registered = await SendAsync(Guid.Empty, new RegisterSellerCommand(AnEmail(), Password, "Mai", "Tran", "Mai Lens"));
        var id = (await SendAsync(registered.Id, new GetMyShopApplicationsQuery())).Single().Id;

        // Waiting already: a second application is refused.
        await Assert.ThrowsAsync<ConflictException>(() =>
            SendAsync(registered.Id, new ApplyForShopCommand("Mai Lens 2", null, null)));

        var rejected = await SendAsync(Moderator, new RejectShopApplicationCommand(id, "Tell us what you sell"), RoleNames.Moderator);
        Assert.Equal(("Rejected", "Tell us what you sell"), (rejected.Status, rejected.DecisionReason));

        var again = await SendAsync(registered.Id, new ApplyForShopCommand("Mai Lens", "Used Fujifilm bodies", null));
        Assert.Equal("Pending", again.Status);
        Assert.Equal(2, (await SendAsync(registered.Id, new GetMyShopApplicationsQuery())).Count);
    }

    [Fact]
    public async Task A_customer_applies_from_their_account_and_a_seller_cannot_apply_again()
    {
        var email = AnEmail();
        var customer = await SendAsync(Guid.Empty, new RegisterCommand(email, Password, "Lan", "Pham"));

        var applied = await SendAsync(customer.Id, new ApplyForShopCommand("Lan Film", null, null));
        Assert.Equal("Pending", applied.Status);

        var seller = await _fixture.ApprovedSellerAsync(AnEmail(), "Already Selling");
        await Assert.ThrowsAsync<ConflictException>(() =>
            SendAsync(seller.Id, new ApplyForShopCommand("A second shop", null, null)));
    }

    [Fact]
    public async Task Staff_see_the_queue_oldest_first_with_who_applied()
    {
        var first = await SendAsync(Guid.Empty, new RegisterSellerCommand(AnEmail(), Password, "First", "Applicant", "First Shop"));
        await SendAsync(Guid.Empty, new RegisterSellerCommand(AnEmail(), Password, "Second", "Applicant", "Second Shop"));

        var page = await SendAsync(Moderator, new GetShopApplicationsQuery("Pending", 1, 50), RoleNames.Moderator);
        var ours = page.Items.Where(a => a.ShopName is "First Shop" or "Second Shop").ToList();

        Assert.Equal(["First Shop", "Second Shop"], ours.Select(a => a.ShopName));
        Assert.Equal(first.Email, ours[0].ApplicantEmail);
        Assert.Equal("First Applicant", ours[0].ApplicantName);
    }

    // ------------------------------------------------------------------ helpers

    private static string AnEmail() => $"shop-{Guid.NewGuid():N}@example.test";

    private static async Task<bool> Attempt(Func<Task> action)
    {
        try
        {
            await action();
            return true;
        }
        catch (ConflictException)
        {
            return false;
        }
    }

    private async Task<bool> HasShopAsync(Guid userId)
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().SellerProfiles.AnyAsync(p => p.UserId == userId);
    }

    private async Task<T> SendAsync<T>(Guid caller, IRequest<T> request, params string[] roles)
    {
        await using var provider = _fixture.For(caller, roles);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task<(List<object> Published, T Result)> PublishedAsync<T>(
        Guid caller, IRequest<T> request, string? role = null, bool expectFailure = false)
    {
        await using var provider = _fixture.For(caller, role is null ? [] : [role]);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        T result = default!;
        await using (var scope = provider.CreateAsyncScope())
        {
            var send = scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
            if (expectFailure)
                await Assert.ThrowsAsync<ConflictException>(() => send);
            else
                result = await send;
        }

        var published = new List<object>();
        published.AddRange(harness.Published.Select<SellerRegisteredEvent>().Select(x => (object)x.Context.Message));
        published.AddRange(harness.Published.Select<UserNotificationRequested>().Select(x => (object)x.Context.Message));
        published.AddRange(harness.Published.Select<AuditEntryRecorded>().Select(x => (object)x.Context.Message));
        return (published, result);
    }
}
