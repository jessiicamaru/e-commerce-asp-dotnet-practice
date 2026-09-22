using Ecommerce.Cart.Application.Carts.Commands.AddToCart;
using Ecommerce.Cart.Application.Carts.Commands.RemoveLine;
using Ecommerce.Cart.Application.Carts.Commands.SetQuantity;
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
