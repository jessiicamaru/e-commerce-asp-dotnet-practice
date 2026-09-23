using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.WebApi.Grpc;
using Ecommerce.Contracts.Grpc;
using Ecommerce.Shared.Money;
using Grpc.Core;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// The pricing answer says whose product each variant is (specs/034), because Order freezes it onto
/// the order line and that is the only way a seller ever learns they sold something.
/// </summary>
/// <remarks>
/// ⚠️ <b>"The shop's own" is an EMPTY seller that is PRESENT</b>, not an absent one. Absent means a
/// Catalog too old to say, and Order logs that rather than writing a real seller's sale down as the
/// shop's. Both halves are asserted, because a Catalog that forgot to set the field for the shop would
/// look identical to one that predates it.
/// </remarks>
[Collection(nameof(CatalogTestCollection))]
public class VariantSellerPricingTests(CatalogTestFixture fixture) : IDisposable
{
    private readonly CatalogTestFixture _fixture = fixture;

    /// <summary>The caller is a singleton for the whole collection; put it back (specs/027).</summary>
    public void Dispose()
    {
        var caller = _fixture.Services.GetRequiredService<TestCaller>();
        caller.Id = Guid.CreateVersion7();
        caller.Roles.Clear();
        caller.Roles.Add("Admin");
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task A_sellers_variant_is_priced_with_its_seller()
    {
        var alice = Guid.CreateVersion7();
        var product = await AsSeller(alice, CreateProductAsync);

        var priced = Assert.Single(await PriceAsync(product.Id));

        Assert.True(priced.HasSellerId);
        Assert.Equal(alice.ToString(), priced.SellerId);
    }

    [Fact]
    public async Task The_shops_own_variant_says_so_with_an_empty_seller_that_is_present()
    {
        var product = await CreateProductAsync();   // as an administrator: the shop's own

        var priced = Assert.Single(await PriceAsync(product.Id));

        Assert.True(priced.HasSellerId, "an unset seller means 'this Catalog cannot say', not 'the shop'");
        Assert.Equal(string.Empty, priced.SellerId);
    }

    /// <summary>
    /// The name a customer's order will keep (specs/036): the shop's name as the sellers read model knows
    /// it, carried on the same answer checkout already asks for.
    /// </summary>
    [Fact]
    public async Task A_sellers_variant_is_priced_with_the_shop_s_name()
    {
        var alice = Guid.CreateVersion7();
        await RecordShopAsync(alice, "Alice Cameras");
        var product = await AsSeller(alice, CreateProductAsync);

        var priced = Assert.Single(await PriceAsync(product.Id));

        Assert.True(priced.HasSellerName);
        Assert.Equal("Alice Cameras", priced.SellerName);
    }

    /// <summary>The shop's own goods carry no shop name: the storefront words them as "the shop".</summary>
    [Fact]
    public async Task The_shop_s_own_variant_carries_no_shop_name()
    {
        var product = await CreateProductAsync();

        Assert.False(Assert.Single(await PriceAsync(product.Id)).HasSellerName);
    }

    /// <summary>
    /// A seller whose registration has not reached Catalog yet has no name to give. It is left unset -
    /// never an empty string or the seller's id, which would print on a customer's order.
    /// </summary>
    [Fact]
    public async Task A_seller_the_read_model_has_not_heard_of_yet_gets_no_name_rather_than_a_placeholder()
    {
        var stranger = Guid.CreateVersion7();
        var product = await AsSeller(stranger, CreateProductAsync);

        var priced = Assert.Single(await PriceAsync(product.Id));

        Assert.False(priced.HasSellerName);
        Assert.Equal(stranger.ToString(), priced.SellerId); // whose it is is still known
    }

    private async Task RecordShopAsync(Guid sellerId, string shopName)
    {
        await using var scope = _fixture.NewScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new Ecommerce.Catalog.Application.Sellers.RecordSellerCommand(sellerId, shopName, DateTime.UtcNow));
    }

    private async Task<IReadOnlyList<PricedVariant>> PriceAsync(params Guid[] variantIds)
    {
        await using var scope = _fixture.NewScope();

        var service = new CatalogPricingService(
            scope.ServiceProvider.GetRequiredService<IProductRepository>(),
            scope.ServiceProvider.GetRequiredService<IOptions<CurrencyOptions>>(),
            NullLogger<CatalogPricingService>.Instance,
            scope.ServiceProvider.GetRequiredService<ISellerRepository>());

        var request = new PriceVariantsRequest();
        request.VariantIds.AddRange(variantIds.Select(id => id.ToString()));

        var response = await service.PriceVariants(request, new BareCallContext());
        return response.Variants;
    }

    private async Task<ProductResponse> CreateProductAsync()
    {
        await using var scope = _fixture.NewScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var categories = await sender.Send(
            new Ecommerce.Catalog.Application.Categories.Queries.GetCategories.GetCategoriesQuery());

        var categoryId = categories.Count > 0
            ? categories[0].Id
            : (await sender.Send(
                new Ecommerce.Catalog.Application.Categories.Commands.CreateCategory.CreateCategoryCommand(
                    $"Sales {Guid.NewGuid():N}"[..24],
                    null,
                    $"sales-{Guid.NewGuid():N}"[..24],
                    null))).Id;

        return await sender.Send(new CreateProductCommand(
            $"Sold camera {Guid.NewGuid():N}"[..28],
            null,
            1_000_000m,
            $"SOLD-{Guid.NewGuid():N}"[..16],
            categoryId));
    }

    private async Task<T> AsSeller<T>(Guid sellerId, Func<Task<T>> body)
    {
        var caller = _fixture.Services.GetRequiredService<TestCaller>();
        caller.Id = sellerId;
        caller.Roles.Clear();
        caller.Roles.Add("Seller");
        return await body();
    }

    /// <summary>
    /// Just enough of a call for the service to read a cancellation token from. The service is called
    /// directly rather than over a channel: what is under test is what it answers, not HTTP/2.
    /// </summary>
    private sealed class BareCallContext : ServerCallContext
    {
        protected override string MethodCore => "PriceVariants";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "test";
        protected override DateTime DeadlineCore => DateTime.MaxValue;
        protected override Metadata RequestHeadersCore { get; } = [];
        protected override CancellationToken CancellationTokenCore => CancellationToken.None;
        protected override Metadata ResponseTrailersCore { get; } = [];
        protected override Status StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore { get; } = new(null, []);

        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) =>
            throw new NotSupportedException();

        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    }
}
