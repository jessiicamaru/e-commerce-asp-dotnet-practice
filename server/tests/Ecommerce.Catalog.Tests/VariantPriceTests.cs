using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Application.Products.Prices;
using Ecommerce.Catalog.Application.Products.Queries.GetProductById;
using Ecommerce.Catalog.Application.Products.Variants.AddProductVariant;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Money;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// A price is an amount somebody decided, in one currency, and the absence of one is an answer
/// (specs/022).
/// </summary>
/// <remarks>
/// <b>Every pair of amounts here is chosen so that no exchange rate could produce both.</b>
/// 40,000,000 dong and 1,499 dollars are not a conversion of each other at any rate anybody would
/// use. If these tests passed against an implementation that converted, the numbers would come out
/// wrong - which is the point, and is why the numbers are not round multiples of each other.
/// </remarks>
[Collection(nameof(CatalogTestCollection))]
public class VariantPriceTests(CatalogTestFixture fixture)
{
    private readonly CatalogTestFixture _fixture = fixture;

    private static readonly Currency Dong = new("VND", 0);
    private static readonly Currency Dollars = new("USD", 2);

    [Fact]
    public async Task A_price_set_in_a_currency_comes_back_exactly_as_entered()
    {
        var product = await CreateProductAsync(price: 40_000_000m);
        var variant = Assert.Single(product.Variants!);

        await SendAsync(new SetVariantPriceCommand(product.Id, variant.Id, "USD", 1499m));

        Assert.Equal(40_000_000m, (await ReadAsync(product.Id, Dong)).Variants![0].Price);

        var inDollars = await ReadAsync(product.Id, Dollars);

        // Not 1,600 (40,000,000 / 25,000) and not 40,000,000. The number that was typed.
        Assert.Equal(1499m, inDollars.Variants![0].Price);
        Assert.Equal("USD", inDollars.Currency);
        Assert.Equal("USD", inDollars.Variants[0].Currency);
    }

    [Fact]
    public async Task A_variant_without_a_price_in_the_currency_is_not_sold_in_it()
    {
        var product = await CreateProductAsync(price: 40_000_000m);
        var variant = Assert.Single(product.Variants!);

        await SendAsync(new SetVariantPriceCommand(product.Id, variant.Id, "USD", 1499m));
        await SendAsync(new RemoveVariantPriceCommand(product.Id, variant.Id, "USD"));

        var inDollars = await ReadAsync(product.Id, Dollars);

        // NOT the dong amount relabelled, NOT a conversion of it, and NOT zero - zero is a price,
        // and it would be a free camera.
        Assert.Null(inDollars.Variants![0].Price);
        Assert.Null(inDollars.Price);
    }

    [Fact]
    public async Task The_default_currency_lives_on_the_variant_and_cannot_be_removed()
    {
        var product = await CreateProductAsync(price: 40_000_000m);
        var variant = Assert.Single(product.Variants!);

        // Setting the default currency writes the variant's own Price - one source for one number.
        await SendAsync(new SetVariantPriceCommand(product.Id, variant.Id, "VND", 38_000_000m));

        Assert.Equal(38_000_000m, (await ReadAsync(product.Id, Dong)).Variants![0].Price);

        // ...and there is no row to remove. Withdrawing a variant is deactivating it, which says so.
        await Assert.ThrowsAsync<ConflictException>(() =>
            SendAsync(new RemoveVariantPriceCommand(product.Id, variant.Id, "VND")));
    }

    [Fact]
    public async Task The_from_price_is_the_cheapest_variant_priced_in_the_currency_asked_for()
    {
        var product = await CreateProductAsync(price: 40_000_000m);
        var body = Assert.Single(product.Variants!);

        var kit = await SendAsync(new AddProductVariantCommand(
            product.Id, $"{product.Sku}-KIT", 52_000_000m, [new VariantOptionInput("Kit", "With 24-105mm")]));

        // The dearer variant in dong is the CHEAPER one in dollars. Nothing proportional about it,
        // which is the point: a "from" price taken from the dong list would name the wrong variant.
        await SendAsync(new SetVariantPriceCommand(product.Id, body.Id, "USD", 1899m));
        await SendAsync(new SetVariantPriceCommand(product.Id, kit.Id, "USD", 1499m));

        Assert.Equal(40_000_000m, (await ReadAsync(product.Id, Dong)).Price);
        Assert.Equal(1499m, (await ReadAsync(product.Id, Dollars)).Price);
    }

    [Fact]
    public async Task A_product_no_variant_of_which_is_priced_has_no_from_price_rather_than_a_converted_one()
    {
        var product = await CreateProductAsync(price: 40_000_000m);

        var inDollars = await ReadAsync(product.Id, Dollars);

        Assert.Null(inDollars.Price);
        Assert.False(inDollars.PriceVaries);
    }

    [Fact]
    public async Task Only_variants_priced_in_the_currency_decide_whether_the_price_varies()
    {
        var product = await CreateProductAsync(price: 40_000_000m);
        var body = Assert.Single(product.Variants!);

        await SendAsync(new AddProductVariantCommand(
            product.Id, $"{product.Sku}-KIT", 52_000_000m, [new VariantOptionInput("Kit", "With 24-105mm")]));

        // Two prices in dong, one in dollars.
        await SendAsync(new SetVariantPriceCommand(product.Id, body.Id, "USD", 1899m));

        Assert.True((await ReadAsync(product.Id, Dong)).PriceVaries);
        Assert.False((await ReadAsync(product.Id, Dollars)).PriceVaries);
    }

    [Fact]
    public async Task A_currency_the_shop_does_not_price_in_is_refused_rather_than_stored()
    {
        var product = await CreateProductAsync(price: 40_000_000m);
        var variant = Assert.Single(product.Variants!);

        // A row nobody would ever read is a claim the shop does not make.
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            SendAsync(new SetVariantPriceCommand(product.Id, variant.Id, "JPY", 220_000m)));
    }

    [Fact]
    public async Task A_price_of_zero_is_refused_because_zero_is_a_price()
    {
        var product = await CreateProductAsync(price: 40_000_000m);
        var variant = Assert.Single(product.Variants!);

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            SendAsync(new SetVariantPriceCommand(product.Id, variant.Id, "USD", 0m)));
    }

    [Fact]
    public async Task An_amount_the_currency_cannot_hold_is_refused()
    {
        var product = await CreateProductAsync(price: 40_000_000m);
        var variant = Assert.Single(product.Variants!);

        // 9.99 dong is not a price. Found against the running stack: an order's tax came out at a
        // whole 3,003 dong while its subtotal was 29.97, because rounding what the system COMPUTES
        // does nothing to an amount somebody TYPED - a subtotal is a unit price times an integer.
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            SendAsync(new SetVariantPriceCommand(product.Id, variant.Id, "VND", 9.99m)));

        // ...and the same amount is perfectly good in a currency that has cents.
        await SendAsync(new SetVariantPriceCommand(product.Id, variant.Id, "USD", 9.99m));
        Assert.Equal(9.99m, (await ReadAsync(product.Id, Dollars)).Variants![0].Price);
    }

    [Fact]
    public async Task Setting_a_price_twice_replaces_it_rather_than_conflicting()
    {
        var product = await CreateProductAsync(price: 40_000_000m);
        var variant = Assert.Single(product.Variants!);

        await SendAsync(new SetVariantPriceCommand(product.Id, variant.Id, "USD", 1499m));
        await SendAsync(new SetVariantPriceCommand(product.Id, variant.Id, "USD", 1399m));

        Assert.Equal(1399m, (await ReadAsync(product.Id, Dollars)).Variants![0].Price);
    }

    [Fact]
    public async Task A_variant_of_somebody_elses_product_is_as_good_as_missing()
    {
        var mine = await CreateProductAsync(price: 40_000_000m);
        var theirs = await CreateProductAsync(price: 12_000_000m);
        var theirVariant = Assert.Single(theirs.Variants!);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            SendAsync(new SetVariantPriceCommand(mine.Id, theirVariant.Id, "USD", 1m)));
    }

    [Fact]
    public async Task Removing_a_price_that_was_never_set_is_not_an_error()
    {
        var product = await CreateProductAsync(price: 40_000_000m);
        var variant = Assert.Single(product.Variants!);

        // The variant is already not sold in dollars, which is what the caller asked for.
        await SendAsync(new RemoveVariantPriceCommand(product.Id, variant.Id, "USD"));
    }

    [Fact]
    public async Task A_request_that_asks_for_nothing_reads_the_default_currency()
    {
        var product = await CreateProductAsync(price: 40_000_000m);

        // The shape every caller had before specs/022, and what a consumer with no request gets.
        var read = await ReadAsync(product.Id, Dong);

        Assert.Equal(40_000_000m, read.Price);
    }

    private async Task<ProductResponse> ReadAsync(Guid productId, Currency currency) =>
        (await SendAsync(new GetProductByIdQuery(productId), currency))!;

    private async Task<ProductResponse> CreateProductAsync(decimal price)
    {
        var categoryId = Guid.CreateVersion7();

        await using (var scope = _fixture.NewScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            context.Categories.Add(new Category
            {
                Id = categoryId,
                Name = $"Cur {categoryId:N}"[..20],
                Slug = $"cur-{categoryId:N}"[..20],
            });
            await context.SaveChangesAsync();
        }

        var sku = $"CUR{Guid.NewGuid():N}"[..20];
        return await SendAsync(new CreateProductCommand($"Camera {sku}", null, price, sku, categoryId), Dong);
    }

    private async Task<T> SendAsync<T>(IRequest<T> request, Currency? currency = null)
    {
        await using var scope = _fixture.NewScope(currency: currency ?? Dong);
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task SendAsync(IRequest request, Currency? currency = null)
    {
        await using var scope = _fixture.NewScope(currency: currency ?? Dong);
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
