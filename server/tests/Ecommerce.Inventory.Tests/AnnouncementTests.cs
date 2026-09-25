using Ecommerce.Contracts.Inventory;
using Ecommerce.Inventory.Application.Reservations.ConfirmStock;
using Ecommerce.Inventory.Application.Reservations.ExpireStock;
using Ecommerce.Inventory.Application.Reservations.ReleaseStock;
using Ecommerce.Inventory.Application.Reservations.ReserveStock;
using Ecommerce.Inventory.Application.Reservations.RestockCancelledOrder;
using Ecommerce.Inventory.Application.Stock.Commands.RegisterProduct;
using Ecommerce.Inventory.Application.Stock.Commands.SetStockOnHand;
using MassTransit.Testing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Inventory.Tests;

/// <summary>
/// One test per path that moves stock.
/// <para>
/// <b>These are the most valuable tests in feature 004, and the reason is worth stating.</b> Six
/// handlers change availability — reserve, release, confirm, expire, set-on-hand and register — and
/// every one has to announce, because Catalog's product listing is now fed entirely by these
/// messages. Nothing enforces that. A path added later - specs/039 added the seventh, restocking a
/// cancelled order - or one of these edited to stop
/// publishing, produces no error anywhere: the listing simply stops updating for that path, and
/// nobody finds out until a shopper buys something that is gone.
/// </para>
/// <para>
/// An EF SaveChanges interceptor would have made the omission impossible. It was rejected as
/// unverified (research D2) — it would publish during <c>SavingChangesAsync</c> while MassTransit's
/// outbox writes through the same DbContext, and whether that works is exactly the kind of thing
/// that appears to and then does not. These tests, one per path, are what stands in for it.
/// </para>
/// </summary>
[Collection(nameof(InventoryTestCollection))]
public class AnnouncementTests(InventoryTestFixture fixture)
{
    private readonly InventoryTestFixture _fixture = fixture;

    [Fact]
    public async Task An_announcement_names_the_variant_it_is_about()
    {
        // What this service counts is a sellable unit, which since specs/020 is a VARIANT. The column
        // is still called ProductId (research D9), so the announcement says the same id twice - once
        // as the product it belongs to, once as the variant Catalog must record it against.
        var variantId = Guid.CreateVersion7();

        await SendAsync(new RegisterProductCommand(variantId, $"SKU-{variantId:N}"[..20]));

        var announcement = await LastAnnouncementForAsync(variantId);

        Assert.Equal(variantId, announcement.VariantId);
        Assert.Equal(announcement.ProductId, announcement.VariantId);
    }

    [Fact]
    public async Task RegisterProduct_announces_that_the_product_is_not_yet_buyable()
    {
        var productId = Guid.CreateVersion7();

        await SendAsync(new RegisterProductCommand(productId, $"SKU-{productId:N}"[..20]));

        var announcement = await LastAnnouncementForAsync(productId);

        Assert.False(announcement.IsAvailable);
        Assert.Equal(0, announcement.QuantityAvailable);
    }

    [Fact]
    public async Task SetStockOnHand_announces_the_new_availability()
    {
        var productId = await RegisteredProductAsync();

        await SendAsync(new SetStockOnHandCommand(productId, 5));

        var announcement = await LastAnnouncementForAsync(productId);

        Assert.True(announcement.IsAvailable);
        Assert.Equal(5, announcement.QuantityAvailable);
    }

    [Fact]
    public async Task ReserveStock_announces_that_the_last_units_are_gone()
    {
        var productId = await StockedProductAsync(onHand: 2);

        await SendAsync(new ReserveStockCommand(
            Guid.CreateVersion7(), [new ReserveStockItem(productId, 2)]));

        var announcement = await LastAnnouncementForAsync(productId);

        Assert.False(announcement.IsAvailable);
        Assert.Equal(0, announcement.QuantityAvailable);
    }

    [Fact]
    public async Task ReleaseStock_announces_that_the_units_are_back()
    {
        var productId = await StockedProductAsync(onHand: 2);
        var orderId = Guid.CreateVersion7();

        await SendAsync(new ReserveStockCommand(orderId, [new ReserveStockItem(productId, 2)]));
        await SendAsync(new ReleaseStockCommand(orderId, "payment declined"));

        var announcement = await LastAnnouncementForAsync(productId);

        Assert.True(announcement.IsAvailable);
        Assert.Equal(2, announcement.QuantityAvailable);
    }

    [Fact]
    public async Task ConfirmStock_announces_the_level_after_the_units_leave()
    {
        var productId = await StockedProductAsync(onHand: 3);
        var orderId = Guid.CreateVersion7();

        await SendAsync(new ReserveStockCommand(orderId, [new ReserveStockItem(productId, 2)]));
        await SendAsync(new ConfirmStockCommand(orderId));

        var announcement = await LastAnnouncementForAsync(productId);

        // On hand falls to 1 and the hold is gone, so one unit is buyable again.
        Assert.True(announcement.IsAvailable);
        Assert.Equal(1, announcement.QuantityAvailable);
    }

    /// <summary>The seventh path (specs/039): a cancelled order's units back on the shelf, announced.</summary>
    [Fact]
    public async Task RestockCancelledOrder_announces_the_units_put_back()
    {
        var productId = await StockedProductAsync(onHand: 2);
        var orderId = Guid.CreateVersion7();

        await SendAsync(new ReserveStockCommand(orderId, [new ReserveStockItem(productId, 2)]));
        await SendAsync(new ConfirmStockCommand(orderId));
        await SendAsync(new RestockCancelledOrderCommand(orderId));

        var announcement = await LastAnnouncementForAsync(productId);

        // Sold out after the confirm; both units buyable again once the order is cancelled.
        Assert.True(announcement.IsAvailable);
        Assert.Equal(2, announcement.QuantityAvailable);
    }

    /// <summary>The eighth path (specs/066): a returned parcel's units back on the shelf, announced.</summary>
    [Fact]
    public async Task RestockReturnedParcel_announces_the_units_put_back()
    {
        var productId = await StockedProductAsync(onHand: 1);
        var orderId = Guid.CreateVersion7();
        await SendAsync(new ReserveStockCommand(orderId, [new ReserveStockItem(productId, 1)]));
        await SendAsync(new ConfirmStockCommand(orderId));

        await SendAsync(new Ecommerce.Inventory.Application.Reservations.RestockReturnedParcel.RestockReturnedParcelCommand(
            Guid.CreateVersion7(), orderId, [new Ecommerce.Contracts.Order.ReturnedItemDto(productId, 1)]));

        var announcement = await LastAnnouncementForAsync(productId);
        Assert.True(announcement.IsAvailable);
        Assert.Equal(1, announcement.QuantityAvailable);
    }

    [Fact]
    public async Task ExpireStock_announces_the_reclaimed_units()
    {
        // The sweeper path — the one most likely to be forgotten, because no user triggers it.
        var productId = await StockedProductAsync(onHand: 4);
        var orderId = Guid.CreateVersion7();

        await SendAsync(new ReserveStockCommand(orderId, [new ReserveStockItem(productId, 4)]));
        await ExpireEveryHoldAsync(orderId);

        await SendAsync(new ExpireStockCommand(BatchSize: 50));

        var announcement = await LastAnnouncementForAsync(productId);

        Assert.True(announcement.IsAvailable);
        Assert.Equal(4, announcement.QuantityAvailable);
    }

    // ---------------------------------------------------------------- helpers

    private async Task<Guid> RegisteredProductAsync()
    {
        var productId = Guid.CreateVersion7();
        await SendAsync(new RegisterProductCommand(productId, $"SKU-{productId:N}"[..20]));
        return productId;
    }

    private async Task<Guid> StockedProductAsync(int onHand)
    {
        var productId = await RegisteredProductAsync();
        await SendAsync(new SetStockOnHandCommand(productId, onHand));
        return productId;
    }

    /// <summary>
    /// Backdates an order's holds so the sweeper has something to reclaim, without waiting out the
    /// configured holding period.
    /// </summary>
    private async Task ExpireEveryHoldAsync(Guid orderId)
    {
        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider
            .GetRequiredService<Infrastructure.Persistence.InventoryDbContext>();

        var reservations = context.StockReservations.Where(r => r.OrderId == orderId).ToList();

        foreach (var reservation in reservations)
        {
            reservation.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// The most recent announcement about one product. Read from the harness's published list
    /// rather than waited for: these commands are dispatched in-process, so by the time
    /// <c>Send</c> returns the message has been published.
    /// </summary>
    private async Task<StockAvailabilityChangedEvent> LastAnnouncementForAsync(Guid productId)
    {
        var published = _fixture.Harness.Published
            .Select<StockAvailabilityChangedEvent>()
            .Select(x => x.Context.Message)
            .Where(m => m.ProductId == productId)
            .ToList();

        Assert.True(
            published.Count > 0,
            $"No StockAvailabilityChangedEvent was published for product {productId}. "
            + "A path that moves stock without announcing it silently freezes the product listing.");

        return await Task.FromResult(published[^1]);
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
