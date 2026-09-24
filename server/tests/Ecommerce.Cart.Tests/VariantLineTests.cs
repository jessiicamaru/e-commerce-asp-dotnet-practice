using Ecommerce.Cart.Application.Carts.Commands.AddToCart;
using Ecommerce.Cart.Application.Carts.Commands.RemoveLine;
using Ecommerce.Cart.Application.Carts.Commands.SetQuantity;
using Ecommerce.Cart.Application.Checkout;
using Ecommerce.Contracts.Order;
using Ecommerce.Cart.Domain.Entities;
using Ecommerce.Cart.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Cart.Tests;

/// <summary>
/// A cart line is a VARIANT: two shapes of one product are two lines, and everything that addresses a
/// line addresses the variant (specs/020).
/// </summary>
[Collection(nameof(CartTestCollection))]
public class VariantLineTests(CartTestFixture fixture)
{
    private readonly CartTestFixture _fixture = fixture;

    [Fact]
    public async Task Two_shapes_of_one_product_are_two_lines()
    {
        var customer = Guid.CreateVersion7();
        var product = Guid.CreateVersion7();
        var black = Guid.CreateVersion7();
        var silver = Guid.CreateVersion7();

        await SendAsync(customer, new AddToCartCommand(product, 1, black));
        await SendAsync(customer, new AddToCartCommand(product, 2, silver));

        var lines = await LinesAsync(customer);

        Assert.Equal(2, lines.Count);
        Assert.Equal(1, lines.Single(l => l.VariantId == black).Quantity);
        Assert.Equal(2, lines.Single(l => l.VariantId == silver).Quantity);
        Assert.All(lines, line => Assert.Equal(product, line.ProductId));
    }

    [Fact]
    public async Task Adding_the_same_shape_again_raises_its_quantity()
    {
        var customer = Guid.CreateVersion7();
        var product = Guid.CreateVersion7();
        var kit = Guid.CreateVersion7();

        await SendAsync(customer, new AddToCartCommand(product, 1, kit));
        await SendAsync(customer, new AddToCartCommand(product, 2, kit));

        var line = Assert.Single(await LinesAsync(customer));
        Assert.Equal(3, line.Quantity);
    }

    [Fact]
    public async Task A_line_added_before_variants_existed_is_still_addressed_by_the_product_id()
    {
        // What a client built before variants sends, and what every line already in the database looks
        // like: no variant, and the product's own id is its only variant's (specs/020 research D2).
        var customer = Guid.CreateVersion7();
        var product = Guid.CreateVersion7();

        await SendAsync(customer, new AddToCartCommand(product, 1));

        var line = Assert.Single(await LinesAsync(customer));
        Assert.Null(line.VariantId);
        Assert.Equal(product, line.SellableId);

        await SendAsync(customer, new SetQuantityCommand(product, 4));
        Assert.Equal(4, Assert.Single(await LinesAsync(customer)).Quantity);

        await SendAsync(customer, new RemoveLineCommand(product));
        Assert.Empty(await LinesAsync(customer));
    }

    [Fact]
    public async Task Removing_one_shape_leaves_the_other()
    {
        var customer = Guid.CreateVersion7();
        var product = Guid.CreateVersion7();
        var black = Guid.CreateVersion7();
        var silver = Guid.CreateVersion7();
        await SendAsync(customer, new AddToCartCommand(product, 1, black));
        await SendAsync(customer, new AddToCartCommand(product, 1, silver));

        await SendAsync(customer, new RemoveLineCommand(black));

        var line = Assert.Single(await LinesAsync(customer));
        Assert.Equal(silver, line.VariantId);
    }

    /// <summary>
    /// #122 (specs/052): a completed order took its lines out by PRODUCT, so with two shapes of one
    /// product in the cart the decrement could land on the one that was not bought.
    /// </summary>
    [Fact]
    public async Task A_completed_order_takes_out_the_variant_it_bought_not_its_sibling()
    {
        var customer = Guid.CreateVersion7();
        var lens = Guid.CreateVersion7();
        var sonyE = Guid.CreateVersion7();
        var fujiX = Guid.CreateVersion7();
        await SendAsync(customer, new AddToCartCommand(lens, 1, sonyE));
        await SendAsync(customer, new AddToCartCommand(lens, 2, fujiX));

        var order = Guid.CreateVersion7();
        await OutcomesAsync(customer, o => o.RecordSubmittedAsync(order, customer, [new(lens, 1, fujiX)]));
        await OutcomesAsync(customer, o => o.RecordCompletedAsync(order));

        var lines = await LinesAsync(customer);
        Assert.Equal(1, lines.Single(l => l.VariantId == sonyE).Quantity);
        Assert.Equal(1, lines.Single(l => l.VariantId == fujiX).Quantity);
    }

    /// <summary>
    /// An item that names no variant - stored before specs/052, or sent by an Order older than specs/020 -
    /// is its product's first variant, whose id IS the product id (specs/020).
    /// </summary>
    [Fact]
    public async Task An_item_naming_no_variant_takes_out_the_line_whose_variant_is_the_product()
    {
        var customer = Guid.CreateVersion7();
        var product = Guid.CreateVersion7();
        var another = Guid.CreateVersion7();
        await SendAsync(customer, new AddToCartCommand(product, 2, product));
        await SendAsync(customer, new AddToCartCommand(product, 1, another));

        var order = Guid.CreateVersion7();
        await OutcomesAsync(customer, o => o.RecordSubmittedAsync(order, customer, [new(product, 2)]));
        await OutcomesAsync(customer, o => o.RecordCompletedAsync(order));

        Assert.Equal(another, Assert.Single(await LinesAsync(customer)).VariantId);
    }

    /// <summary>A line from before variants (no VariantId) is its product's first variant too.</summary>
    [Fact]
    public async Task A_line_from_before_variants_is_matched_by_its_product()
    {
        var customer = Guid.CreateVersion7();
        var product = Guid.CreateVersion7();
        await SendAsync(customer, new AddToCartCommand(product, 3));

        var order = Guid.CreateVersion7();
        await OutcomesAsync(customer, o => o.RecordSubmittedAsync(order, customer, [new(product, 1, product)]));
        await OutcomesAsync(customer, o => o.RecordCompletedAsync(order));

        Assert.Equal(2, Assert.Single(await LinesAsync(customer)).Quantity);
    }

    /// <summary>The event carried the variant all along; the consumer's mapping is where it was lost.</summary>
    [Fact]
    public void The_variant_travels_from_the_event_and_an_empty_one_falls_back_to_the_product()
    {
        var product = Guid.CreateVersion7();
        var variant = Guid.CreateVersion7();

        Assert.Equal(variant, OrderedItem.From(new OrderItemDto(product, 2, 10m, variant)).Sellable);
        Assert.Equal(product, OrderedItem.From(new OrderItemDto(product, 2, 10m)).Sellable);
        Assert.Equal(2, OrderedItem.From(new OrderItemDto(product, 2, 10m, variant)).Quantity);
    }

    private async Task OutcomesAsync(Guid customer, Func<CheckoutOutcomes, Task> act)
    {
        await using var provider = _fixture.For(customer);
        await using var scope = provider.CreateAsyncScope();
        await act(scope.ServiceProvider.GetRequiredService<CheckoutOutcomes>());
    }

    private async Task<List<CartLine>> LinesAsync(Guid customer)
    {
        await using var provider = _fixture.For(customer);
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CartDbContext>();

        return await context.Carts
            .Where(c => c.UserId == customer)
            .SelectMany(c => c.Lines)
            .AsNoTracking()
            .ToListAsync();
    }

    private async Task SendAsync(Guid customer, IRequest request)
    {
        await using var provider = _fixture.For(customer);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
