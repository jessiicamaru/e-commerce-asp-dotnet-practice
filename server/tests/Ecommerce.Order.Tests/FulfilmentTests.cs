using Ecommerce.Order.Application.Orders.Commands.Fulfilment;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Shared.Exceptions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// Paid → Preparing → Shipped, moved by staff - and nothing else, however it is asked (feature 011).
/// </summary>
/// <remarks>
/// Every refusal asserts the row is unchanged, including <c>UpdatedAt</c>: a guard that "refused" but
/// still wrote would pass a status-only check.
/// </remarks>
[Collection(nameof(OrderTestCollection))]
public class FulfilmentTests(OrderTestFixture fixture)
{
    private readonly OrderTestFixture _fixture = fixture;

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    [Fact]
    public async Task A_paid_order_is_prepared_then_shipped_with_its_tracking_reference()
    {
        var id = await OrderSeed.OrderInAsync(_fixture, OrderStatus.Paid);

        var prepared = await SendAsync(new PrepareOrderCommand(id));
        Assert.Equal("Preparing", prepared.Status);

        var shipped = await SendAsync(new ShipOrderCommand(id, "VN123"));
        Assert.Equal("Shipped", shipped.Status);
        Assert.Equal("VN123", shipped.TrackingReference);

        var row = await OrderSeed.ReadAsync(_fixture, id);
        Assert.Equal(OrderStatus.Shipped, row.Status);
        Assert.Equal("VN123", row.TrackingReference);
    }

    [Fact]
    public async Task A_legacy_Completed_order_is_prepared_like_a_paid_one_and_reads_as_Paid()
    {
        var id = await OrderSeed.OrderInAsync(_fixture, OrderStatus.Completed);

        var prepared = await SendAsync(new PrepareOrderCommand(id));

        Assert.Equal("Preparing", prepared.Status);
    }

    [Fact]
    public async Task Preparing_twice_is_a_no_op_not_an_error()
    {
        var id = await OrderSeed.OrderInAsync(_fixture, OrderStatus.Paid);
        await SendAsync(new PrepareOrderCommand(id));
        var afterFirst = await OrderSeed.ReadAsync(_fixture, id);

        var again = await SendAsync(new PrepareOrderCommand(id));

        Assert.Equal("Preparing", again.Status);
        Assert.Equal(afterFirst.UpdatedAt, (await OrderSeed.ReadAsync(_fixture, id)).UpdatedAt);
    }

    [Fact]
    public async Task Shipping_again_with_the_same_reference_is_a_no_op_but_a_different_one_is_refused()
    {
        var id = await OrderSeed.OrderInAsync(_fixture, OrderStatus.Preparing);
        await SendAsync(new ShipOrderCommand(id, "VN123"));

        var same = await SendAsync(new ShipOrderCommand(id, "VN123"));
        Assert.Equal("VN123", same.TrackingReference);

        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new ShipOrderCommand(id, "VN999")));
        Assert.Equal("VN123", (await OrderSeed.ReadAsync(_fixture, id)).TrackingReference);
    }

    [Theory]
    [InlineData(OrderStatus.Shipped)]     // backwards
    [InlineData(OrderStatus.Failed)]      // a failed order never enters fulfilment
    [InlineData(OrderStatus.Submitted)]   // not paid yet
    public async Task Preparing_is_refused_from_anything_but_Paid_and_changes_nothing(OrderStatus from)
    {
        var id = await OrderSeed.OrderInAsync(_fixture, from, tracking: from == OrderStatus.Shipped ? "VN1" : null);
        var before = await OrderSeed.ReadAsync(_fixture, id);

        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new PrepareOrderCommand(id)));

        var after = await OrderSeed.ReadAsync(_fixture, id);
        Assert.Equal(from, after.Status);
        Assert.Equal(before.UpdatedAt, after.UpdatedAt);
    }

    [Fact]
    public async Task Shipping_is_refused_for_a_paid_order_that_was_never_prepared()
    {
        var id = await OrderSeed.OrderInAsync(_fixture, OrderStatus.Paid);

        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new ShipOrderCommand(id, "VN123")));

        Assert.Equal(OrderStatus.Paid, (await OrderSeed.ReadAsync(_fixture, id)).Status);
    }

    [Fact]
    public async Task Ten_concurrent_prepare_requests_move_the_order_exactly_once()
    {
        var id = await OrderSeed.OrderInAsync(_fixture, OrderStatus.Paid);

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => SendAsync(new PrepareOrderCommand(id))));

        Assert.All(results, r => Assert.Equal("Preparing", r.Status));
        Assert.Equal(OrderStatus.Preparing, (await OrderSeed.ReadAsync(_fixture, id)).Status);
    }

    [Fact]
    public async Task An_unknown_order_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new PrepareOrderCommand(Guid.CreateVersion7())));
    }
}
