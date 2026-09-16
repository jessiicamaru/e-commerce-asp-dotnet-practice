using Ecommerce.Contracts.Inventory;
using Ecommerce.Inventory.Application.Common.Interfaces;
using Ecommerce.Inventory.Application.Reservations.ReserveStock;
using Ecommerce.Inventory.Application.Stock.Commands.RegisterProduct;
using Ecommerce.Inventory.Application.Stock.Commands.SetStockOnHand;
using Ecommerce.Inventory.Domain.Enums;
using MassTransit.Testing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Inventory.Tests;

[Collection(nameof(InventoryTestCollection))]
public class ReserveStockTests(InventoryTestFixture fixture)
{
    private readonly InventoryTestFixture _fixture = fixture;

    private async Task<Guid> StockedProductAsync(int onHand)
    {
        var productId = Guid.CreateVersion7();

        await using var scope = _fixture.NewScope();
        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

        await mediator.Send(new RegisterProductCommand(productId, $"SKU-{productId:N}"[..20]));

        if (onHand > 0)
        {
            await mediator.Send(new SetStockOnHandCommand(productId, onHand));
        }

        return productId;
    }

    private async Task<ReserveStockResult> ReserveAsync(Guid orderId, params (Guid ProductId, int Quantity)[] lines)
    {
        await using var scope = _fixture.NewScope();
        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

        return await mediator.Send(new ReserveStockCommand(
            orderId,
            lines.Select(l => new ReserveStockItem(l.ProductId, l.Quantity)).ToList()));
    }

    private async Task<(int OnHand, int Reserved)> ReadStockAsync(Guid productId)
    {
        await using var scope = _fixture.NewScope();
        var repository = scope.ServiceProvider.GetRequiredService<IStockRepository>();
        var stock = await repository.GetByProductIdAsync(productId);

        return (stock!.QuantityOnHand, stock.QuantityReserved);
    }

    // ---------- User Story 1 ----------

    [Fact]
    public async Task Reserving_available_stock_succeeds_and_holds_the_units()
    {
        var productId = await StockedProductAsync(10);
        var orderId = Guid.CreateVersion7();

        var result = await ReserveAsync(orderId, (productId, 3));

        Assert.True(result.Succeeded);

        var (onHand, reserved) = await ReadStockAsync(productId);
        Assert.Equal(10, onHand);
        Assert.Equal(3, reserved);
    }

    [Fact]
    public async Task Reserving_publishes_exactly_one_reply_and_never_both()
    {
        var productId = await StockedProductAsync(5);
        var orderId = Guid.CreateVersion7();

        await ReserveAsync(orderId, (productId, 2));

        // FR-002: every request gets exactly one answer.
        Assert.True(await _fixture.Harness.Published.Any<InventoryReservedEvent>(
            x => x.Context.Message.OrderId == orderId));

        Assert.False(await _fixture.Harness.Published.Any<InventoryReservationFailedEvent>(
            x => x.Context.Message.OrderId == orderId));
    }

    [Fact]
    public async Task Duplicate_lines_for_one_product_are_summed_not_judged_separately()
    {
        var productId = await StockedProductAsync(5);

        // 4 + 4 = 8 against 5 available. Evaluated line by line, both would pass.
        var result = await ReserveAsync(Guid.CreateVersion7(), (productId, 4), (productId, 4));

        Assert.False(result.Succeeded);

        var (_, reserved) = await ReadStockAsync(productId);
        Assert.Equal(0, reserved);
    }

    // ---------- User Story 2: rejection ----------

    [Fact]
    public async Task Insufficient_stock_is_rejected_with_a_reason_naming_the_product()
    {
        var productId = await StockedProductAsync(2);

        var result = await ReserveAsync(Guid.CreateVersion7(), (productId, 5));

        Assert.False(result.Succeeded);
        Assert.Contains("Insufficient stock", result.FailureReason);
        Assert.Contains(productId.ToString(), result.FailureReason);

        var (_, reserved) = await ReadStockAsync(productId);
        Assert.Equal(0, reserved);
    }

    [Fact]
    public async Task An_unknown_product_is_unfulfillable_rather_than_unlimited()
    {
        var result = await ReserveAsync(Guid.CreateVersion7(), (Guid.CreateVersion7(), 1));

        Assert.False(result.Succeeded);
        Assert.Contains("Unknown product", result.FailureReason);
    }

    [Fact]
    public async Task A_non_positive_quantity_is_rejected()
    {
        var productId = await StockedProductAsync(10);

        var result = await ReserveAsync(Guid.CreateVersion7(), (productId, 0));

        Assert.False(result.Succeeded);
        Assert.Contains("Invalid quantity", result.FailureReason);
    }

    [Fact]
    public async Task An_order_is_all_or_nothing_across_its_lines()
    {
        var available = await StockedProductAsync(10);
        var scarce = await StockedProductAsync(1);

        var result = await ReserveAsync(Guid.CreateVersion7(), (available, 2), (scarce, 5));

        Assert.False(result.Succeeded);

        // FR-003: the available line must not be left half-reserved.
        Assert.Equal(0, (await ReadStockAsync(available)).Reserved);
        Assert.Equal(0, (await ReadStockAsync(scarce)).Reserved);
    }

    // ---------- Idempotency (FR-006, SC-005) ----------

    [Fact]
    public async Task Replaying_a_reservation_moves_stock_once()
    {
        var productId = await StockedProductAsync(10);
        var orderId = Guid.CreateVersion7();

        await ReserveAsync(orderId, (productId, 3));
        await ReserveAsync(orderId, (productId, 3));
        await ReserveAsync(orderId, (productId, 3));

        var (_, reserved) = await ReadStockAsync(productId);
        Assert.Equal(3, reserved);

        await using var scope = _fixture.NewScope();
        var reservations = scope.ServiceProvider.GetRequiredService<IReservationRepository>();
        Assert.Single(await reservations.GetByOrderIdAsync(orderId));
    }

    // ---------- Concurrency (SC-004) ----------

    [Fact]
    public async Task A_hundred_concurrent_orders_against_ten_units_sell_exactly_ten()
    {
        var productId = await StockedProductAsync(10);

        var attempts = Enumerable.Range(0, 100)
            .Select(_ => ReserveAsync(Guid.CreateVersion7(), (productId, 1)))
            .ToArray();

        var results = await Task.WhenAll(attempts);

        // Every request got an outcome — none left indeterminate (SC-001).
        Assert.Equal(100, results.Length);

        Assert.Equal(10, results.Count(r => r.Succeeded));
        Assert.Equal(90, results.Count(r => !r.Succeeded));

        var (onHand, reserved) = await ReadStockAsync(productId);
        Assert.Equal(10, onHand);
        Assert.Equal(10, reserved);
    }

    // ---------- At rest (SC-008) ----------

    [Fact]
    public async Task With_nothing_in_flight_reserved_returns_to_zero()
    {
        var productId = await StockedProductAsync(4);
        var orderId = Guid.CreateVersion7();

        await ReserveAsync(orderId, (productId, 4));

        await using var scope = _fixture.NewScope();
        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
        await mediator.Send(new Application.Reservations.ReleaseStock.ReleaseStockCommand(orderId, "test"));

        var (onHand, reserved) = await ReadStockAsync(productId);
        Assert.Equal(0, reserved);
        Assert.Equal(4, onHand);

        var reservations = scope.ServiceProvider.GetRequiredService<IReservationRepository>();
        var rows = await reservations.GetByOrderIdAsync(orderId);
        Assert.All(rows, r => Assert.Equal(ReservationStatus.Released, r.Status));
    }
}
