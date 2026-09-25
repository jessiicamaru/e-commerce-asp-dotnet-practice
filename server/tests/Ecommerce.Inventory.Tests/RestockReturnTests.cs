using Ecommerce.Contracts.Order;
using Ecommerce.Inventory.Application.Common.Interfaces;
using Ecommerce.Inventory.Application.Reservations.ConfirmStock;
using Ecommerce.Inventory.Application.Reservations.ReserveStock;
using Ecommerce.Inventory.Application.Reservations.RestockReturnedParcel;
using Ecommerce.Inventory.Application.Stock.Commands.RegisterProduct;
using Ecommerce.Inventory.Application.Stock.Commands.SetStockOnHand;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Inventory.Tests;

/// <summary>
/// A returned parcel's units go back on the shelf (specs/066) - what the event names, once, however often it
/// is delivered.
/// </summary>
[Collection(nameof(InventoryTestCollection))]
public class RestockReturnTests(InventoryTestFixture fixture)
{
    private readonly InventoryTestFixture _fixture = fixture;

    [Fact]
    public async Task A_returned_parcel_puts_its_units_back_on_hand()
    {
        var (variant, order) = await SoldAsync(onHand: 5, quantity: 2);
        Assert.Equal((3, 0), await ReadStockAsync(variant));

        var restocked = await SendAsync(new RestockReturnedParcelCommand(Guid.CreateVersion7(), order, [new ReturnedItemDto(variant, 2)]));

        Assert.Equal(2, restocked);
        Assert.Equal((5, 0), await ReadStockAsync(variant));
    }

    /// <summary>Redelivered, or delivered twice at once: once.</summary>
    [Fact]
    public async Task A_return_delivered_many_times_restocks_once()
    {
        var (variant, order) = await SoldAsync(onHand: 5, quantity: 2);
        var returnId = Guid.CreateVersion7();

        var restocked = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            SendAsync(new RestockReturnedParcelCommand(returnId, order, [new ReturnedItemDto(variant, 2)]))));

        Assert.Equal(2, restocked.Sum());
        Assert.Equal((5, 0), await ReadStockAsync(variant));
    }

    [Fact]
    public async Task Two_returns_of_one_order_both_come_back()
    {
        var (variant, order) = await SoldAsync(onHand: 5, quantity: 2);

        await SendAsync(new RestockReturnedParcelCommand(Guid.CreateVersion7(), order, [new ReturnedItemDto(variant, 1)]));
        await SendAsync(new RestockReturnedParcelCommand(Guid.CreateVersion7(), order, [new ReturnedItemDto(variant, 1)]));

        Assert.Equal((5, 0), await ReadStockAsync(variant));
    }

    [Fact]
    public async Task A_variant_with_no_stock_row_is_skipped_not_invented()
    {
        var restocked = await SendAsync(new RestockReturnedParcelCommand(Guid.CreateVersion7(), Guid.CreateVersion7(),
            [new ReturnedItemDto(Guid.CreateVersion7(), 3)]));

        Assert.Equal(0, restocked);
    }

    private async Task<(Guid Variant, Guid Order)> SoldAsync(int onHand, int quantity)
    {
        var variant = Guid.CreateVersion7();
        var order = Guid.CreateVersion7();
        await using var scope = _fixture.NewScope();
        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
        await mediator.Send(new RegisterProductCommand(variant, $"SKU-{variant:N}"[..20]));
        await mediator.Send(new SetStockOnHandCommand(variant, onHand));
        await mediator.Send(new ReserveStockCommand(order, [new ReserveStockItem(variant, quantity)]));
        await mediator.Send(new ConfirmStockCommand(order));
        return (variant, order);
    }

    private async Task<(int OnHand, int Reserved)> ReadStockAsync(Guid variant)
    {
        await using var scope = _fixture.NewScope();
        var stock = await scope.ServiceProvider.GetRequiredService<IStockRepository>().GetByProductIdAsync(variant);
        return (stock!.QuantityOnHand, stock.QuantityReserved);
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
