using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Application.Sellers;
using Ecommerce.Contracts.Activity;
using Ecommerce.Contracts.Identity;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MassTransit.Testing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// A seller describes their shop (#197, specs/099): stored on their profile, announced to Catalog in the same save, and
/// cleared by saving it empty.
/// </summary>
[Collection(nameof(IdentityTestCollection))]
public class ShopDescriptionTests(IdentityTestFixture fixture)
{
    private readonly IdentityTestFixture _fixture = fixture;

    [Fact]
    public async Task A_seller_describes_their_shop_and_Catalog_is_told_with_the_save()
    {
        var seller = await _fixture.ApprovedSellerAsync($"desc-{Guid.NewGuid():N}@example.test", "Mai Lens");

        var (shop, described, audited) = await DescribeAsync(seller.Id, "  Used Fujifilm bodies.  ");

        Assert.Equal("Used Fujifilm bodies.", shop.Description);
        Assert.Equal((seller.Id, "Used Fujifilm bodies."), (described!.SellerId, described.Description));
        Assert.Equal("ShopDescribed", audited!.Action);
        Assert.Equal("Used Fujifilm bodies.", (await SendAsync(seller.Id, new GetMyShopQuery())).Description);
    }

    [Fact]
    public async Task Saving_it_empty_clears_it()
    {
        var seller = await _fixture.ApprovedSellerAsync($"desc-{Guid.NewGuid():N}@example.test", "Mai Lens");
        await DescribeAsync(seller.Id, "Something");

        var (shop, described, _) = await DescribeAsync(seller.Id, "   ");

        Assert.Null(shop.Description);
        Assert.Null(described!.Description);
    }

    [Fact]
    public async Task Too_long_is_refused_and_somebody_who_does_not_sell_has_no_shop_to_describe()
    {
        var seller = await _fixture.ApprovedSellerAsync($"desc-{Guid.NewGuid():N}@example.test", "Mai Lens");
        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(seller.Id, new DescribeShopCommand(new string('x', 501))));

        var customer = await SendAsync(Guid.Empty, new RegisterCommand($"desc-{Guid.NewGuid():N}@example.test", "Passw0rd!23", "Lan", "Pham"));
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(customer.Id, new DescribeShopCommand("Mine")));
    }

    // ------------------------------------------------------------------ helpers

    private async Task<(SellerProfileResponse, SellerDescribedEvent?, AuditEntryRecorded?)> DescribeAsync(Guid seller, string? description)
    {
        await using var provider = _fixture.For(seller, ["Seller"]);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        SellerProfileResponse shop;
        await using (var scope = provider.CreateAsyncScope())
        {
            shop = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new DescribeShopCommand(description));
        }

        return (shop,
            harness.Published.Select<SellerDescribedEvent>().Select(x => x.Context.Message).SingleOrDefault(),
            harness.Published.Select<AuditEntryRecorded>().Select(x => x.Context.Message).SingleOrDefault());
    }

    private async Task<T> SendAsync<T>(Guid caller, IRequest<T> request)
    {
        await using var provider = _fixture.For(caller, ["Seller"]);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
