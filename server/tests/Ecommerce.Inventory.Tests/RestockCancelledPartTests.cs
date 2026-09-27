using Ecommerce.Inventory.Application.Common.Interfaces;
using Ecommerce.Inventory.Application.Reservations.ConfirmStock;
using Ecommerce.Inventory.Application.Reservations.ReserveStock;
using Ecommerce.Inventory.Application.Reservations.RestockCancelledOrder;
using Ecommerce.Inventory.Application.Stock.Commands.RegisterProduct;
using Ecommerce.Inventory.Application.Stock.Commands.SetStockOnHand;
using Ecommerce.Inventory.Domain.Enums;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Inventory.Tests;

/// <summary>
/// One part of an order cancelled on its own (specs/104): that part's variants come back - released if still held,
/// back on hand if confirmed - and the rest of the order's units stay where they are.
/// </summary>
[Collection(nameof(InventoryTestCollection))]
public class RestockCancelledPartTests(InventoryTestFixture fixture)
{
    private readonly InventoryTestFixture _fixture = fixture;

    [Fact]
    public async Task A_confirmed_part_goes_back_on_hand_and_the_rest_stays_sold()
    {
        var (mine, theirs) = (await StockedAsync(10), await StockedAsync(10));
        var order = await ReserveAsync((mine, 2), (theirs, 3));
        await SendAsync(new ConfirmStockCommand(order));   // 8 and 7 on hand, nothing held

        Assert.Equal(1, await SendAsync(new RestockCancelledOrderCommand(order, [mine])));

        Assert.Equal((10, 0), await ReadAsync(mine));
        Assert.Equal((7, 0), await ReadAsync(theirs));
        var rows = await ReservationsAsync(order);
        Assert.Equal((ReservationStatus.Released, "Returned: part cancelled"), rows.Where(r => r.ProductId == mine).Select(r => (r.Status, r.SettlementReason)).Single());
        Assert.Equal(ReservationStatus.Confirmed, rows.Single(r => r.ProductId == theirs).Status);
    }

    /// <summary>The order is Paid in Order before Inventory has necessarily confirmed: a held part is released, and the
    /// late completion then confirms only what is still held.</summary>
    [Fact]
    public async Task A_part_cancelled_before_the_completion_releases_its_hold_and_the_completion_takes_only_the_rest()
    {
        var (mine, theirs) = (await StockedAsync(10), await StockedAsync(10));
        var order = await ReserveAsync((mine, 2), (theirs, 3));

        await SendAsync(new RestockCancelledOrderCommand(order, [mine]));
        Assert.Equal((10, 0), await ReadAsync(mine));
        Assert.Equal((10, 3), await ReadAsync(theirs));

        await SendAsync(new ConfirmStockCommand(order));
        Assert.Equal((10, 0), await ReadAsync(mine));   // nothing taken for the cancelled part
        Assert.Equal((7, 0), await ReadAsync(theirs));
    }

    [Fact]
    public async Task A_redelivery_moves_nothing_and_a_later_whole_cancel_returns_only_the_rest()
    {
        var (mine, theirs) = (await StockedAsync(10), await StockedAsync(10));
        var order = await ReserveAsync((mine, 2), (theirs, 3));
        await SendAsync(new ConfirmStockCommand(order));

        await SendAsync(new RestockCancelledOrderCommand(order, [mine]));
        Assert.Equal(0, await SendAsync(new RestockCancelledOrderCommand(order, [mine])));
        Assert.Equal((10, 0), await ReadAsync(mine));

        await SendAsync(new RestockCancelledOrderCommand(order));   // the rest cancelled too
        Assert.Equal((10, 0), await ReadAsync(mine));   // not twice
        Assert.Equal((10, 0), await ReadAsync(theirs));
    }

    // ------------------------------------------------------------------ helpers

    private async Task<Guid> StockedAsync(int onHand)
    {
        var variant = Guid.CreateVersion7();
        await SendAsync(new RegisterProductCommand(variant, $"PART-{variant:N}"[..20]));
        await SendAsync(new SetStockOnHandCommand(variant, onHand));
        return variant;
    }

    private async Task<Guid> ReserveAsync(params (Guid Variant, int Quantity)[] lines)
    {
        var order = Guid.CreateVersion7();
        var result = await SendAsync(new ReserveStockCommand(order, lines.Select(l => new ReserveStockItem(l.Variant, l.Quantity)).ToList()));
        Assert.True(result.Succeeded, result.FailureReason);
        return order;
    }

    private async Task<(int OnHand, int Reserved)> ReadAsync(Guid variant)
    {
        await using var scope = _fixture.NewScope();
        var stock = await scope.ServiceProvider.GetRequiredService<IStockRepository>().GetByProductIdAsync(variant);
        return (stock!.QuantityOnHand, stock.QuantityReserved);
    }

    private async Task<List<Domain.Entities.StockReservation>> ReservationsAsync(Guid order)
    {
        await using var scope = _fixture.NewScope();
        var repository = scope.ServiceProvider.GetRequiredService<IReservationRepository>();
        return [
            .. await repository.GetByOrderIdAndStatusAsync(order, ReservationStatus.Released),
            .. await repository.GetByOrderIdAndStatusAsync(order, ReservationStatus.Confirmed),
            .. await repository.GetByOrderIdAndStatusAsync(order, ReservationStatus.Held),
        ];
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
