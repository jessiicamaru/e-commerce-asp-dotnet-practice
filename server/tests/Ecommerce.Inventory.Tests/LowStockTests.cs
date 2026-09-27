using Ecommerce.Contracts.Inventory;
using Ecommerce.Inventory.Application.Common;
using Ecommerce.Inventory.Application.Reservations.ReleaseStock;
using Ecommerce.Inventory.Application.Reservations.ReserveStock;
using Ecommerce.Inventory.Application.Stock.Commands.RegisterProduct;
using Ecommerce.Inventory.Application.Stock.Commands.SetLowStockThreshold;
using Ecommerce.Inventory.Application.Stock.Commands.SetStockOnHand;
using Ecommerce.Inventory.Application.Stock.Queries.GetStockByProductId;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MassTransit.Testing;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Inventory.Tests;

/// <summary>
/// A seller is told when a sale takes a variant below its low-stock line (#200, specs/102): once per crossing, judged
/// from the available stock before and after under the reservation's lock, never for a seller's own adjustment.
/// </summary>
[Collection(nameof(InventoryTestCollection))]
public class LowStockTests(InventoryTestFixture fixture) : IDisposable
{
    private readonly InventoryTestFixture _fixture = fixture;

    public void Dispose()
    {
        _fixture.Caller.BeAdmin();
        _fixture.Owners.Clear();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task A_sale_that_crosses_the_line_publishes_once_and_further_sales_below_it_do_not()
    {
        var variant = await StockedAsync(6);   // the default line is 5

        await ReserveAsync(variant, 2);         // 6 -> 4: crossed
        await ReserveAsync(variant, 1);         // 4 -> 3: already below

        var low = Assert.Single(RanLow(variant));
        Assert.Equal((4, 5), (low.QuantityAvailable, low.Threshold));
    }

    [Fact]
    public async Task Stock_back_above_the_line_makes_the_next_fall_a_new_crossing()
    {
        var variant = await StockedAsync(6);
        var failed = await ReserveAsync(variant, 2);            // 6 -> 4
        await SendAsync(new ReleaseStockCommand(failed, "Payment declined"));   // back to 6
        await ReserveAsync(variant, 3);                          // 6 -> 3

        Assert.Equal([4, 3], RanLow(variant).Select(e => e.QuantityAvailable));
    }

    [Fact]
    public async Task Exactly_at_the_line_is_not_low_and_a_sale_to_nothing_is_one_crossing()
    {
        var atLine = await StockedAsync(7);
        await ReserveAsync(atLine, 2);          // 7 -> 5: at the line, not below
        Assert.Empty(RanLow(atLine));

        var sellOut = await StockedAsync(6);
        await ReserveAsync(sellOut, 6);         // 6 -> 0
        Assert.Equal(0, Assert.Single(RanLow(sellOut)).QuantityAvailable);
    }

    [Fact]
    public async Task A_variants_own_line_overrides_the_default_and_zero_never_tells()
    {
        var own = await StockedAsync(12);
        await SendAsync(new SetLowStockThresholdCommand(own, 10));
        await ReserveAsync(own, 3);             // 12 -> 9: below its own 10, above the default 5
        Assert.Equal(10, Assert.Single(RanLow(own)).Threshold);

        var off = await StockedAsync(6);
        await SendAsync(new SetLowStockThresholdCommand(off, 0));
        await ReserveAsync(off, 6);
        Assert.Empty(RanLow(off));
    }

    [Fact]
    public async Task A_seller_lowering_their_own_stock_is_not_told_and_a_redelivery_publishes_nothing()
    {
        var variant = await StockedAsync(6);
        await SendAsync(new SetStockOnHandCommand(variant, 2));   // they did it themselves
        Assert.Empty(RanLow(variant));

        var other = await StockedAsync(6);
        var order = Guid.CreateVersion7();
        await ReserveAsync(other, 2, order);
        await ReserveAsync(other, 2, order);   // the broker delivers the same reservation again
        Assert.Single(RanLow(other));
    }

    [Fact]
    public async Task Several_variants_in_one_order_are_judged_each_on_its_own()
    {
        var (a, b, c) = (await StockedAsync(6), await StockedAsync(20), await StockedAsync(5));

        await SendAsync(new ReserveStockCommand(Guid.CreateVersion7(),
            [new ReserveStockItem(a, 2), new ReserveStockItem(b, 2), new ReserveStockItem(c, 1)]));

        Assert.Single(RanLow(a));   // 6 -> 4
        Assert.Empty(RanLow(b));    // 20 -> 18
        Assert.Single(RanLow(c));   // 5 -> 4: at the line is not below, so 5 counts as above
    }

    [Fact]
    public async Task A_seller_sets_the_line_of_their_own_variant_only_and_within_range()
    {
        var alice = Guid.CreateVersion7();
        var mine = await StockedAsync(3);
        var theirs = await StockedAsync(3);
        _fixture.Owners.OwnedBy(mine, alice);
        _fixture.Owners.OwnedBy(theirs, Guid.CreateVersion7());
        _fixture.Caller.BeSeller(alice);

        var set = await SendAsync(new SetLowStockThresholdCommand(mine, 10));
        Assert.Equal((10, false), (set.LowStockThreshold, set.LowStockThresholdIsDefault));

        var cleared = await SendAsync(new SetLowStockThresholdCommand(mine, null));
        Assert.Equal((5, true), (cleared.LowStockThreshold, cleared.LowStockThresholdIsDefault));
        Assert.Equal(5, (await SendAsync(new GetStockByProductIdQuery(mine))).LowStockThreshold);

        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new SetLowStockThresholdCommand(theirs, 10)));
        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(new SetLowStockThresholdCommand(mine, -1)));
        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(new SetLowStockThresholdCommand(mine, 100_001)));
    }

    [Theory]
    [InlineData(6, 4, 5, true)]
    [InlineData(5, 4, 5, true)]
    [InlineData(4, 3, 5, false)]
    [InlineData(7, 5, 5, false)]
    [InlineData(6, 0, 0, false)]
    public void The_crossing_is_from_at_or_above_to_strictly_below(int before, int after, int threshold, bool crossed) =>
        Assert.Equal(crossed, LowStock.Crossed(before, after, threshold));

    [Theory]
    [InlineData(null, 5)]
    [InlineData("12", 12)]
    [InlineData("0", 0)]
    public void The_default_line_comes_from_configuration(string? configured, int expected) =>
        Assert.Equal(expected, LowStockSettings.From(Config(configured)).DefaultThreshold);

    [Theory]
    [InlineData("-1")]
    [InlineData("100001")]
    [InlineData("five")]
    public void A_line_the_service_cannot_use_stops_it_at_startup(string configured) =>
        Assert.Contains(LowStockSettings.Key,
            Assert.Throws<InvalidOperationException>(() => LowStockSettings.From(Config(configured))).Message);

    // ------------------------------------------------------------------ helpers

    private static IConfiguration Config(string? value) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { [LowStockSettings.Key] = value })
        .Build();

    private List<StockRanLowEvent> RanLow(Guid variant) =>
        _fixture.Harness.Published.Select<StockRanLowEvent>().Select(x => x.Context.Message)
            .Where(e => e.VariantId == variant).ToList();

    private async Task<Guid> StockedAsync(int onHand)
    {
        var variant = Guid.CreateVersion7();
        await SendAsync(new RegisterProductCommand(variant, $"LOW-{variant:N}"[..20]));
        await SendAsync(new SetStockOnHandCommand(variant, onHand));
        return variant;
    }

    private async Task<Guid> ReserveAsync(Guid variant, int quantity, Guid? orderId = null)
    {
        var order = orderId ?? Guid.CreateVersion7();
        var result = await SendAsync(new ReserveStockCommand(order, [new ReserveStockItem(variant, quantity)]));
        Assert.True(result.Succeeded, result.FailureReason);
        return order;
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
