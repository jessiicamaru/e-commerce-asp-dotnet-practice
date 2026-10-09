using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Application.Products.Prices;
using Ecommerce.Catalog.Application.Products.Queries.GetProductById;
using Ecommerce.Catalog.Application.Products.Queries.GetProducts;
using Ecommerce.Catalog.Application.Products.Variants.UpdateProductVariant;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Catalog.WebApi.Grpc;
using Ecommerce.Contracts.Grpc;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Money;
using FluentValidation;
using Grpc.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// A compare-at price per variant (specs/161, #369): shown struck through, never charged - on a real PostgreSQL, because
/// "above the price" is a CHECK the database enforces and "on sale" is a query it answers.
/// </summary>
[Collection(nameof(CatalogTestCollection))]
public class CompareAtPriceTests(CatalogTestFixture fixture) : IDisposable
{
    private readonly CatalogTestFixture _fixture = fixture;
    private static readonly Currency Dong = new("VND", 0);
    private static readonly Currency Dollars = new("USD", 2);

    public void Dispose()
    {
        var caller = _fixture.Services.GetRequiredService<TestCaller>();
        caller.Id = Guid.CreateVersion7();
        caller.Roles.Clear();
        caller.Roles.Add("Admin");
    }

    [Fact]
    public async Task A_compare_at_reads_back_on_the_variant_and_on_the_card()
    {
        var (productId, variantId) = await CreateAsync(990_000m);

        var set = await SendAsync(new SetCompareAtPriceCommand(productId, variantId, "VND", 1_200_000m));
        Assert.Equal(990_000m, set.Price);
        Assert.Equal(1_200_000m, set.CompareAtPrice);

        var product = (await SendAsync(new GetProductByIdQuery(productId), Dong))!;
        Assert.Equal(1_200_000m, product.CompareAtPrice);
        Assert.Equal(1_200_000m, Assert.Single(product.Variants!).CompareAtPrice);

        // Never converted: asked in dollars, a variant with no dollar price shows no reduction (specs/022).
        var inDollars = (await SendAsync(new GetProductByIdQuery(productId), Dollars))!;
        Assert.Null(inDollars.CompareAtPrice);
    }

    [Fact]
    public async Task The_card_shows_the_compare_at_of_the_variant_whose_price_it_shows()
    {
        var (productId, cheap) = await CreateAsync(990_000m);
        var dear = await AddVariantAsync(productId, 2_000_000m);
        // A reduction on the dearer shape is not a reduction of the "from" price.
        await SendAsync(new SetCompareAtPriceCommand(productId, dear, "VND", 2_500_000m));

        Assert.Null((await SendAsync(new GetProductByIdQuery(productId), Dong))!.CompareAtPrice);

        await SendAsync(new SetCompareAtPriceCommand(productId, cheap, "VND", 1_100_000m));
        Assert.Equal(1_100_000m, (await SendAsync(new GetProductByIdQuery(productId), Dong))!.CompareAtPrice);
    }

    [Theory]
    [InlineData("VND", 990_000)]        // equal to the price
    [InlineData("VND", 500_000)]        // below it
    [InlineData("VND", 1_200_000.5)]    // dong has no decimal places
    [InlineData("USD", 99)]             // no dollar price to compare against
    [InlineData("EUR", 99)]             // not a currency the shop prices in
    public async Task A_compare_at_that_is_not_a_reduction_is_refused(string currency, decimal amount)
    {
        var (productId, variantId) = await CreateAsync(990_000m);

        await Assert.ThrowsAsync<ValidationException>(() =>
            SendAsync(new SetCompareAtPriceCommand(productId, variantId, currency, amount)));

        Assert.Null((await VariantAsync(variantId)).CompareAtPrice);
    }

    [Fact]
    public async Task The_database_refuses_a_compare_at_not_above_its_price()
    {
        var (_, variantId) = await CreateAsync(990_000m);

        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var variant = await context.ProductVariants.SingleAsync(v => v.Id == variantId);
        variant.CompareAtPrice = 990_000m;

        var refused = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Contains("CK_product_variants_CompareAtPrice", refused.InnerException!.Message);
    }

    [Fact]
    public async Task A_price_raised_to_its_compare_at_clears_it_and_a_lowered_one_keeps_it()
    {
        var (productId, variantId) = await CreateAsync(990_000m);
        await SendAsync(new SetVariantPriceCommand(productId, variantId, "USD", 39m));
        await SendAsync(new SetCompareAtPriceCommand(productId, variantId, "VND", 1_200_000m));
        await SendAsync(new SetCompareAtPriceCommand(productId, variantId, "USD", 49m));

        // Lowered: still reduced, so still compared.
        await SendAsync(new SetVariantPriceCommand(productId, variantId, "VND", 900_000m));
        Assert.Equal(1_200_000m, (await VariantAsync(variantId)).CompareAtPrice);

        // Raised to it, in either currency and through either endpoint: no longer a reduction, so it goes.
        await SendAsync(new SetVariantPriceCommand(productId, variantId, "USD", 49m));
        await SendAsync(new UpdateProductVariantCommand(productId, variantId, 1_300_000m, true));

        var variant = await VariantAsync(variantId);
        Assert.Null(variant.CompareAtPrice);
        Assert.Null(Assert.Single(variant.Prices).CompareAtAmount);
    }

    [Fact]
    public async Task Clearing_takes_it_away_and_clearing_nothing_is_quiet()
    {
        var (productId, variantId) = await CreateAsync(990_000m);
        await SendAsync(new SetCompareAtPriceCommand(productId, variantId, "VND", 1_200_000m));

        await SendAsync(new ClearCompareAtPriceCommand(productId, variantId, "VND"));
        await SendAsync(new ClearCompareAtPriceCommand(productId, variantId, "VND"));

        Assert.Null((await VariantAsync(variantId)).CompareAtPrice);
    }

    [Fact]
    public async Task On_sale_lists_exactly_the_reduced_products_in_the_currency_asked_for()
    {
        var category = await CategoryAsync();
        var (reduced, reducedVariant) = await CreateAsync(990_000m, category);
        var (inDollarsOnly, dollarVariant) = await CreateAsync(990_000m, category);
        var (plain, _) = await CreateAsync(990_000m, category);
        await SendAsync(new SetCompareAtPriceCommand(reduced, reducedVariant, "VND", 1_200_000m));
        await SendAsync(new SetVariantPriceCommand(inDollarsOnly, dollarVariant, "USD", 39m));
        await SendAsync(new SetCompareAtPriceCommand(inDollarsOnly, dollarVariant, "USD", 49m));

        Assert.Equal([reduced], await OnSaleAsync(category, Dong));
        Assert.Equal([inDollarsOnly], await OnSaleAsync(category, Dollars));
        Assert.Equal(3, (await SendAsync(new GetProductsQuery(CategoryId: category, PageSize: 50), Dong)).Items.Count);
        Assert.DoesNotContain(plain, await OnSaleAsync(category, Dong));
    }

    [Fact]
    public async Task Checkout_is_charged_the_price_and_never_reads_the_compare_at()
    {
        var (productId, variantId) = await CreateAsync(990_000m);
        await SendAsync(new SetCompareAtPriceCommand(productId, variantId, "VND", 1_200_000m));

        var priced = await PriceAsync(variantId);

        Assert.True(priced.Sellable);
        Assert.Equal("990000", decimal.Parse(priced.Price, System.Globalization.CultureInfo.InvariantCulture).ToString("0"));
    }

    [Fact]
    public async Task A_seller_sets_one_on_their_approved_product_without_sending_it_to_review_and_not_on_anothers()
    {
        var seller = Guid.CreateVersion7();
        var (productId, variantId) = await CreateAsync(990_000m, sellerId: seller);

        As(seller, "Seller");
        await SendAsync(new SetCompareAtPriceCommand(productId, variantId, "VND", 1_200_000m));
        Assert.Equal(ProductReviewStatus.Approved, (await ProductAsync(productId)).ReviewStatus);

        As(Guid.CreateVersion7(), "Seller");
        await Assert.ThrowsAsync<NotFoundException>(() =>
            SendAsync(new SetCompareAtPriceCommand(productId, variantId, "VND", 1_300_000m)));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            SendAsync(new ClearCompareAtPriceCommand(productId, variantId, "VND")));
    }

    private async Task<List<Guid>> OnSaleAsync(Guid category, Currency currency) =>
        (await SendAsync(new GetProductsQuery(CategoryId: category, PageSize: 50, OnSale: true), currency)).Items.Select(p => p.Id).ToList();

    private async Task<(Guid ProductId, Guid VariantId)> CreateAsync(decimal price, Guid? category = null, Guid? sellerId = null)
    {
        category ??= await CategoryAsync();
        var sku = $"CMP{Guid.NewGuid():N}"[..20];
        var product = await SendAsync(new CreateProductCommand($"Sale {sku}", null, price, sku, category.Value));

        if (sellerId is not null)
        {
            await using var scope = _fixture.NewScope();
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await context.Products.Where(p => p.Id == product.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(p => p.SellerId, sellerId));
        }

        return (product.Id, product.Variants?.Single().Id ?? product.Id);
    }

    private async Task<Guid> AddVariantAsync(Guid productId, decimal price)
    {
        var sku = $"CMV{Guid.NewGuid():N}"[..20];
        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var variant = new ProductVariant
        {
            Id = Guid.CreateVersion7(),
            ProductId = productId,
            Sku = sku,
            Price = price,
            OptionSummary = "Large",
            IsActive = true,
        };
        context.ProductVariants.Add(variant);
        await context.SaveChangesAsync();
        return variant.Id;
    }

    private async Task<Guid> CategoryAsync()
    {
        var id = Guid.CreateVersion7();
        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        context.Categories.Add(new Category { Id = id, Name = $"Cmp {id:N}"[..20], Slug = $"cmp-{id:N}"[..20] });
        await context.SaveChangesAsync();
        return id;
    }

    private async Task<ProductVariant> VariantAsync(Guid variantId)
    {
        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        return await context.ProductVariants.AsNoTracking().Include(v => v.Prices).SingleAsync(v => v.Id == variantId);
    }

    private async Task<Product> ProductAsync(Guid productId)
    {
        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        return await context.Products.AsNoTracking().SingleAsync(p => p.Id == productId);
    }

    private async Task<PricedVariant> PriceAsync(Guid variantId)
    {
        await using var scope = _fixture.NewScope();
        var service = new CatalogPricingService(
            scope.ServiceProvider.GetRequiredService<IProductRepository>(),
            scope.ServiceProvider.GetRequiredService<IOptions<CurrencyOptions>>(),
            NullLogger<CatalogPricingService>.Instance,
            scope.ServiceProvider.GetRequiredService<ISellerRepository>());
        var request = new PriceVariantsRequest();
        request.VariantIds.Add(variantId.ToString());
        return (await service.PriceVariants(request, new BareCallContext())).Variants.Single();
    }

    private void As(Guid id, string role)
    {
        var caller = _fixture.Services.GetRequiredService<TestCaller>();
        caller.Id = id;
        caller.Roles.Clear();
        caller.Roles.Add(role);
    }

    private async Task<T> SendAsync<T>(IRequest<T> request, Currency? currency = null)
    {
        await using var scope = _fixture.NewScope(currency: currency);
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task SendAsync(IRequest request)
    {
        await using var scope = _fixture.NewScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    /// <summary>Just enough of a call for the pricing service to read a cancellation token from.</summary>
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
