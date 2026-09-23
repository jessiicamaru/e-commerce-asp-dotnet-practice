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
/// A cancelled order puts its stock back (specs/039) - the first compensation after a saga has
/// finished. SC-001: on hand and reserved end exactly where they were before the order.
/// </summary>
[Collection(nameof(InventoryTestCollection))]
public class RestockTests(InventoryTestFixture fixture)
{
    private readonly InventoryTestFixture _fixture = fixture;

    [Fact]
    public async Task A_cancelled_order_that_completed_puts_its_units_back_on_hand()
    {
        var (productId, orderId) = await ReservedAsync(onHand: 5, quantity: 2);
        await SendAsync(new ConfirmStockCommand(orderId));
        Assert.Equal((3, 0), await ReadStockAsync(productId));

        var settled = await SendAsync(new RestockCancelledOrderCommand(orderId));

        Assert.Equal(1, settled);
        Assert.Equal((5, 0), await ReadStockAsync(productId));
        var reservation = Assert.Single(await ReservationsAsync(orderId));
        Assert.Equal(ReservationStatus.Released, reservation.Status);
        Assert.Contains("cancelled", reservation.SettlementReason);
    }

    /// <summary>
    /// research D4: the cancellation can arrive BEFORE the completion - two message types, no order between
    /// them. The hold is released instead, and the late completion then finds nothing to deduct.
    /// </summary>
    [Fact]
    public async Task A_cancellation_that_arrives_before_the_completion_releases_the_hold()
    {
        var (productId, orderId) = await ReservedAsync(onHand: 5, quantity: 2);

        await SendAsync(new RestockCancelledOrderCommand(orderId));
        Assert.Equal((5, 0), await ReadStockAsync(productId));

        Assert.Equal(0, await SendAsync(new ConfirmStockCommand(orderId)));
        Assert.Equal((5, 0), await ReadStockAsync(productId));
    }

    /// <summary>Redelivered: the second finds nothing Confirmed or Held and moves nothing.</summary>
    [Fact]
    public async Task Restocking_twice_puts_the_units_back_once()
    {
        var (productId, orderId) = await ReservedAsync(onHand: 5, quantity: 2);
        await SendAsync(new ConfirmStockCommand(orderId));

        await SendAsync(new RestockCancelledOrderCommand(orderId));
        Assert.Equal(0, await SendAsync(new RestockCancelledOrderCommand(orderId)));

        Assert.Equal((5, 0), await ReadStockAsync(productId));
    }

    /// <summary>Several lines, and someone else's hold on the same product, untouched.</summary>
    [Fact]
    public async Task Only_this_order_s_units_come_back()
    {
        var productId = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();
        await using (var scope = _fixture.NewScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
            await mediator.Send(new RegisterProductCommand(productId, $"SKU-{productId:N}"[..20]));
            await mediator.Send(new SetStockOnHandCommand(productId, 10));
        }

        var orderId = Guid.CreateVersion7();
        await SendAsync(new ReserveStockCommand(orderId, [new ReserveStockItem(productId, 3)]));
        await SendAsync(new ConfirmStockCommand(orderId));
        await SendAsync(new ReserveStockCommand(other, [new ReserveStockItem(productId, 2)]));

        await SendAsync(new RestockCancelledOrderCommand(orderId));

        Assert.Equal((10, 2), await ReadStockAsync(productId));
    }

    [Fact]
    public async Task An_order_this_service_never_reserved_for_is_a_no_op()
    {
        Assert.Equal(0, await SendAsync(new RestockCancelledOrderCommand(Guid.CreateVersion7())));
    }

    // ------------------------------------------------------------------ helpers

    private async Task<(Guid ProductId, Guid OrderId)> ReservedAsync(int onHand, int quantity)
    {
        var productId = Guid.CreateVersion7();
        var orderId = Guid.CreateVersion7();

        await using var scope = _fixture.NewScope();
        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
        await mediator.Send(new RegisterProductCommand(productId, $"SKU-{productId:N}"[..20]));
        await mediator.Send(new SetStockOnHandCommand(productId, onHand));
        await mediator.Send(new ReserveStockCommand(orderId, [new ReserveStockItem(productId, quantity)]));

        return (productId, orderId);
    }

    private async Task<(int OnHand, int Reserved)> ReadStockAsync(Guid productId)
    {
        await using var scope = _fixture.NewScope();
        var stock = await scope.ServiceProvider.GetRequiredService<IStockRepository>().GetByProductIdAsync(productId);
        return (stock!.QuantityOnHand, stock.QuantityReserved);
    }

    private async Task<List<Domain.Entities.StockReservation>> ReservationsAsync(Guid orderId)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<IReservationRepository>().GetByOrderIdAsync(orderId);
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
