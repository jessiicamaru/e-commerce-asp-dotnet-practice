using Ecommerce.Inventory.Application.Stock.Commands.ForgetProduct;
using Ecommerce.Inventory.Application.Stock.Commands.RegisterProduct;
using Ecommerce.Inventory.Application.Stock.Commands.SetStockOnHand;
using Ecommerce.Inventory.Application.Stock.Queries.GetStockByProductId;
using Ecommerce.Shared.Exceptions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Inventory.Tests;

/// <summary>
/// What happens to a count when the catalogue forgets what it was counting (specs/024).
/// </summary>
/// <remarks>
/// The mirror of registering. Without it the stock row outlives the product: <c>GET /api/stock/{id}</c>
/// keeps answering for something no catalogue has heard of, and whatever was on the shelf stays there.
/// </remarks>
[Collection(nameof(InventoryTestCollection))]
public class ForgetProductTests(InventoryTestFixture fixture)
{
    private readonly InventoryTestFixture _fixture = fixture;

    [Fact]
    public async Task A_forgotten_variant_has_no_stock_to_answer_for()
    {
        var variantId = Guid.CreateVersion7();
        await SendAsync(new RegisterProductCommand(variantId, $"SKU-{variantId:N}"[..20]));
        await SendAsync(new SetStockOnHandCommand(variantId, 25));

        var forgotten = await SendAsync(new ForgetProductCommand([variantId]));

        Assert.Equal(1, forgotten);

        // Not "zero on hand" - gone. Zero would mean the catalogue still sells it and there are none
        // left, which is a different fact and the one a shopper would be shown.
        await Assert.ThrowsAsync<NotFoundException>(() =>
            SendAsync(new GetStockByProductIdQuery(variantId)));
    }

    [Fact]
    public async Task Every_variant_of_the_product_is_forgotten_not_just_the_first()
    {
        // A product's first variant reuses the product's id and the rest do not (specs/020), so a
        // command carrying one id would leave the kit's units on the shelf forever.
        var body = Guid.CreateVersion7();
        var kit = Guid.CreateVersion7();

        foreach (var id in new[] { body, kit })
        {
            await SendAsync(new RegisterProductCommand(id, $"SKU-{id:N}"[..20]));
            await SendAsync(new SetStockOnHandCommand(id, 5));
        }

        Assert.Equal(2, await SendAsync(new ForgetProductCommand([body, kit])));
    }

    [Fact]
    public async Task Forgetting_twice_is_a_no_op_because_a_message_redelivers()
    {
        var variantId = Guid.CreateVersion7();
        await SendAsync(new RegisterProductCommand(variantId, $"SKU-{variantId:N}"[..20]));

        Assert.Equal(1, await SendAsync(new ForgetProductCommand([variantId])));

        // Zero is the honest answer to "forget this", not a failure: it is already forgotten.
        Assert.Equal(0, await SendAsync(new ForgetProductCommand([variantId])));
    }

    [Fact]
    public async Task Forgetting_one_variant_leaves_every_other_count_alone()
    {
        var doomed = Guid.CreateVersion7();
        var bystander = Guid.CreateVersion7();

        foreach (var id in new[] { doomed, bystander })
        {
            await SendAsync(new RegisterProductCommand(id, $"SKU-{id:N}"[..20]));
            await SendAsync(new SetStockOnHandCommand(id, 7));
        }

        await SendAsync(new ForgetProductCommand([doomed]));

        var survivor = await SendAsync(new GetStockByProductIdQuery(bystander));
        Assert.Equal(7, survivor.QuantityOnHand);
    }

    [Fact]
    public async Task An_empty_list_touches_nothing()
    {
        var bystander = Guid.CreateVersion7();
        await SendAsync(new RegisterProductCommand(bystander, $"SKU-{bystander:N}"[..20]));

        Assert.Equal(0, await SendAsync(new ForgetProductCommand([])));
        Assert.NotNull(await SendAsync(new GetStockByProductIdQuery(bystander)));
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
