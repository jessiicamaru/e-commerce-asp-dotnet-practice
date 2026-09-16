using Ecommerce.Inventory.Application.Common.Interfaces;
using Ecommerce.Inventory.Application.Reservations.ConfirmStock;
using Ecommerce.Inventory.Application.Reservations.ExpireStock;
using Ecommerce.Inventory.Application.Reservations.ReleaseStock;
using Ecommerce.Inventory.Application.Reservations.ReserveStock;
using Ecommerce.Inventory.Application.Stock.Commands.RegisterProduct;
using Ecommerce.Inventory.Application.Stock.Commands.SetStockOnHand;
using Ecommerce.Inventory.Domain.Enums;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Inventory.Tests;

/// <summary>Release, confirm and expiry — the three ways a hold ends.</summary>
[Collection(nameof(InventoryTestCollection))]
public class SettlementTests(InventoryTestFixture fixture)
{
    private readonly InventoryTestFixture _fixture = fixture;

    private async Task<(Guid ProductId, Guid OrderId)> HeldReservationAsync(int onHand, int quantity)
    {
        var productId = Guid.CreateVersion7();
        var orderId = Guid.CreateVersion7();

        await using var scope = _fixture.NewScope();
        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

        await mediator.Send(new RegisterProductCommand(productId, $"SKU-{productId:N}"[..20]));
        await mediator.Send(new SetStockOnHandCommand(productId, onHand));
        await mediator.Send(new ReserveStockCommand(
            orderId, [new ReserveStockItem(productId, quantity)]));

        return (productId, orderId);
    }

    private async Task<(int OnHand, int Reserved)> ReadStockAsync(Guid productId)
    {
        await using var scope = _fixture.NewScope();
        var repository = scope.ServiceProvider.GetRequiredService<IStockRepository>();
        var stock = await repository.GetByProductIdAsync(productId);

        return (stock!.QuantityOnHand, stock.QuantityReserved);
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task<List<Domain.Entities.StockReservation>> ReservationsAsync(Guid orderId)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider
            .GetRequiredService<IReservationRepository>()
            .GetByOrderIdAsync(orderId);
    }

    // ---------- User Story 3: release ----------

    [Fact]
    public async Task Releasing_returns_the_units_without_touching_quantity_on_hand()
    {
        var (productId, orderId) = await HeldReservationAsync(onHand: 10, quantity: 3);

        await SendAsync(new ReleaseStockCommand(orderId, "Payment failed"));

        var (onHand, reserved) = await ReadStockAsync(productId);
        Assert.Equal(10, onHand);
        Assert.Equal(0, reserved);

        var reservation = Assert.Single(await ReservationsAsync(orderId));
        Assert.Equal(ReservationStatus.Released, reservation.Status);
        Assert.Equal("Payment failed", reservation.SettlementReason);
    }

    [Fact]
    public async Task Releasing_twice_does_not_credit_the_units_twice()
    {
        var (productId, orderId) = await HeldReservationAsync(onHand: 10, quantity: 3);

        await SendAsync(new ReleaseStockCommand(orderId, "first"));
        await SendAsync(new ReleaseStockCommand(orderId, "second"));
        await SendAsync(new ReleaseStockCommand(orderId, "third"));

        var (onHand, reserved) = await ReadStockAsync(productId);
        Assert.Equal(10, onHand);
        Assert.Equal(0, reserved);
    }

    [Fact]
    public async Task Releasing_an_order_that_was_never_reserved_is_a_silent_no_op()
    {
        var released = await SendAsync(new ReleaseStockCommand(Guid.CreateVersion7(), "nothing here"));

        Assert.Equal(0, released);
    }

    // ---------- Confirmation, closing the research D2 gap ----------

    [Fact]
    public async Task Confirming_removes_the_units_from_stock_permanently()
    {
        var (productId, orderId) = await HeldReservationAsync(onHand: 10, quantity: 3);

        await SendAsync(new ConfirmStockCommand(orderId));

        // The distinction that matters: confirmed units leave the building, released units return
        // to the shelf.
        var (onHand, reserved) = await ReadStockAsync(productId);
        Assert.Equal(7, onHand);
        Assert.Equal(0, reserved);

        var reservation = Assert.Single(await ReservationsAsync(orderId));
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
    }

    [Fact]
    public async Task Confirming_twice_deducts_once()
    {
        var (productId, orderId) = await HeldReservationAsync(onHand: 10, quantity: 3);

        await SendAsync(new ConfirmStockCommand(orderId));
        await SendAsync(new ConfirmStockCommand(orderId));

        var (onHand, _) = await ReadStockAsync(productId);
        Assert.Equal(7, onHand);
    }

    [Fact]
    public async Task A_release_arriving_after_a_confirmation_changes_nothing()
    {
        var (productId, orderId) = await HeldReservationAsync(onHand: 10, quantity: 3);

        await SendAsync(new ConfirmStockCommand(orderId));
        await SendAsync(new ReleaseStockCommand(orderId, "late release"));

        // The units are gone; a late release must not put them back on the shelf.
        var (onHand, reserved) = await ReadStockAsync(productId);
        Assert.Equal(7, onHand);
        Assert.Equal(0, reserved);
    }

    // ---------- User Story 4: expiry ----------

    [Fact]
    public async Task A_reservation_inside_its_holding_period_is_left_alone()
    {
        var (productId, _) = await HeldReservationAsync(onHand: 10, quantity: 3);

        var expired = await SendAsync(new ExpireStockCommand());

        Assert.Equal(0, expired);
        Assert.Equal(3, (await ReadStockAsync(productId)).Reserved);
    }

    [Fact]
    public async Task A_reservation_past_its_holding_period_is_reclaimed()
    {
        var (productId, orderId) = await HeldReservationAsync(onHand: 10, quantity: 3);

        await ExpireNowAsync(orderId);

        var reclaimed = await SendAsync(new ExpireStockCommand());

        Assert.Equal(1, reclaimed);

        var (onHand, reserved) = await ReadStockAsync(productId);
        Assert.Equal(10, onHand);
        Assert.Equal(0, reserved);

        var reservation = Assert.Single(await ReservationsAsync(orderId));
        Assert.Equal(ReservationStatus.Expired, reservation.Status);
    }

    [Fact]
    public async Task A_settlement_arriving_after_expiry_changes_nothing()
    {
        var (productId, orderId) = await HeldReservationAsync(onHand: 10, quantity: 3);

        await ExpireNowAsync(orderId);
        await SendAsync(new ExpireStockCommand());

        // FR-017: the saga may still send either settlement afterwards.
        await SendAsync(new ReleaseStockCommand(orderId, "late release"));
        await SendAsync(new ConfirmStockCommand(orderId));

        var (onHand, reserved) = await ReadStockAsync(productId);
        Assert.Equal(10, onHand);
        Assert.Equal(0, reserved);
    }

    /// <summary>Moves a reservation's deadline into the past, standing in for the clock.</summary>
    private async Task ExpireNowAsync(Guid orderId)
    {
        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider
            .GetRequiredService<Infrastructure.Persistence.InventoryDbContext>();

        var rows = context.StockReservations.Where(x => x.OrderId == orderId);

        foreach (var row in rows)
        {
            row.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        }

        await context.SaveChangesAsync();
    }
}
