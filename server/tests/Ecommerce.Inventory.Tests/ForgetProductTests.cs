using Ecommerce.Inventory.Application.Common.Interfaces;
using Ecommerce.Inventory.Application.Reservations.ConfirmStock;
using Ecommerce.Inventory.Application.Reservations.ExpireStock;
using Ecommerce.Inventory.Application.Reservations.ReserveStock;
using Ecommerce.Inventory.Application.Reservations.RestockCancelledOrder;
using Ecommerce.Inventory.Application.Stock.Commands.ForgetProduct;
using Ecommerce.Inventory.Domain.Entities;
using Ecommerce.Inventory.Domain.Enums;
using Ecommerce.Inventory.Application.Stock.Commands.RegisterProduct;
using Ecommerce.Inventory.Application.Stock.Commands.SetStockOnHand;
using Ecommerce.Inventory.Application.Stock.Queries.GetStockByProductId;
using Ecommerce.Shared.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
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

    // ---------- #181 (specs/090): the reservations a deleted variant leaves behind ----------

    [Fact]
    public async Task A_held_reservation_of_a_deleted_variant_is_released_with_its_stock()
    {
        // There is no foreign key from stock_reservations to stock_items, so nothing cascaded: the hold stayed
        // Held, for the sweeper or the order's settlement to "return" units to a shelf that no longer exists.
        var (variantId, orderId) = await HeldAsync(onHand: 10, quantity: 3);

        await SendAsync(new ForgetProductCommand([variantId]));

        var reservation = Assert.Single(await ReservationsAsync(orderId));
        Assert.Equal(ReservationStatus.Released, reservation.Status);
        Assert.Equal(ForgetProductCommandHandler.Reason, reservation.SettlementReason);
        Assert.NotNull(reservation.SettledAt);

        // Nothing is left for the order's settlement to confirm.
        Assert.Equal(0, await SendAsync(new ConfirmStockCommand(orderId)));
    }

    [Fact]
    public async Task Only_the_deleted_variants_holds_are_released_and_the_rest_of_the_order_settles()
    {
        var doomed = await StockedAsync(onHand: 10);
        var kept = await StockedAsync(onHand: 10);
        var orderId = Guid.CreateVersion7();
        await SendAsync(new ReserveStockCommand(orderId, [new ReserveStockItem(doomed, 2), new ReserveStockItem(kept, 4)]));

        await SendAsync(new ForgetProductCommand([doomed]));
        Assert.Equal(1, await SendAsync(new ConfirmStockCommand(orderId)));

        var byVariant = (await ReservationsAsync(orderId)).ToDictionary(r => r.ProductId);
        Assert.Equal(ReservationStatus.Released, byVariant[doomed].Status);
        Assert.Equal(ReservationStatus.Confirmed, byVariant[kept].Status);
        var survivor = await SendAsync(new GetStockByProductIdQuery(kept));
        Assert.Equal((6, 0), (survivor.QuantityOnHand, survivor.QuantityReserved));
    }

    [Fact]
    public async Task A_settled_reservation_stays_as_the_orders_history()
    {
        var (variantId, orderId) = await HeldAsync(onHand: 10, quantity: 3);
        await SendAsync(new ConfirmStockCommand(orderId));

        await SendAsync(new ForgetProductCommand([variantId]));

        var reservation = Assert.Single(await ReservationsAsync(orderId));
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        Assert.NotEqual(ForgetProductCommandHandler.Reason, reservation.SettlementReason);
    }

    [Fact]
    public async Task A_cancellation_after_the_variant_was_deleted_settles_without_a_shelf_to_return_to()
    {
        // The one path that still reaches a settled reservation: a paid order cancelled later (specs/039). It must
        // neither throw nor invent a stock row for a variant the catalogue has deleted.
        var (variantId, orderId) = await HeldAsync(onHand: 10, quantity: 3);
        await SendAsync(new ConfirmStockCommand(orderId));
        await SendAsync(new ForgetProductCommand([variantId]));

        await SendAsync(new RestockCancelledOrderCommand(orderId));

        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new GetStockByProductIdQuery(variantId)));
        Assert.Equal(ReservationStatus.Released, Assert.Single(await ReservationsAsync(orderId)).Status);
    }

    [Fact]
    public async Task The_sweeper_finds_nothing_of_a_deleted_variant_to_expire()
    {
        var (variantId, orderId) = await HeldAsync(onHand: 10, quantity: 3);
        await SendAsync(new ForgetProductCommand([variantId]));
        await ExpireNowAsync(orderId);

        await SendAsync(new ExpireStockCommand());

        Assert.Equal(ReservationStatus.Released, Assert.Single(await ReservationsAsync(orderId)).Status);
    }

    private async Task<Guid> StockedAsync(int onHand)
    {
        var variantId = Guid.CreateVersion7();
        await SendAsync(new RegisterProductCommand(variantId, $"SKU-{variantId:N}"[..20]));
        await SendAsync(new SetStockOnHandCommand(variantId, onHand));
        return variantId;
    }

    private async Task<(Guid VariantId, Guid OrderId)> HeldAsync(int onHand, int quantity)
    {
        var variantId = await StockedAsync(onHand);
        var orderId = Guid.CreateVersion7();
        await SendAsync(new ReserveStockCommand(orderId, [new ReserveStockItem(variantId, quantity)]));
        return (variantId, orderId);
    }

    private async Task<List<StockReservation>> ReservationsAsync(Guid orderId)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<IReservationRepository>().GetByOrderIdAsync(orderId);
    }

    private async Task ExpireNowAsync(Guid orderId)
    {
        await using var scope = _fixture.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<Ecommerce.Inventory.Infrastructure.Persistence.InventoryDbContext>();
        await db.StockReservations.Where(r => r.OrderId == orderId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
