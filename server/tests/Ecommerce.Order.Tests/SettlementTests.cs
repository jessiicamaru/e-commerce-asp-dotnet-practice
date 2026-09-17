using Ecommerce.Contracts.Order;
using Ecommerce.Order.Application.Orders.Commands.CompleteOrder;
using Ecommerce.Order.Application.Orders.Commands.FailOrder;
using Ecommerce.Order.Domain.Entities;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Order.Infrastructure.Persistence;
using MassTransit.Testing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// What happens to an order row when the checkout reports its outcome — including when it reports it
/// more than once, and when two reports disagree.
/// </summary>
[Collection(nameof(OrderTestCollection))]
public class SettlementTests(OrderTestFixture fixture)
{
    private readonly OrderTestFixture _fixture = fixture;

    // ---------------------------------------------------------------- User Story 1

    [Fact]
    public async Task OrderCompletedEvent_settles_a_submitted_order()
    {
        var orderId = await SeedSubmittedOrderAsync();
        var completedAt = DateTime.UtcNow;

        // Through the broker, so this covers the consumer wiring as well as the handler.
        await _fixture.Harness.Bus.Publish(new OrderCompletedEvent(orderId, completedAt));

        var order = await WaitForSettlementAsync(orderId);

        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Null(order.FailureReason);
        Assert.True(order.UpdatedAt > order.CreatedAt);
    }

    [Fact]
    public async Task Ten_completion_notices_settle_the_order_exactly_once()
    {
        var orderId = await SeedSubmittedOrderAsync();
        var completedAt = new DateTime(2026, 9, 16, 10, 0, 0, DateTimeKind.Utc);

        var settledCount = 0;

        // Ten deliveries of the same notice, which is what at-least-once delivery means in
        // practice. Dispatched directly rather than published, so the assertion is about the guard
        // and not about how quickly a broker drains a queue.
        for (var i = 0; i < 10; i++)
        {
            if (await SendAsync(new CompleteOrderCommand(orderId, completedAt)))
            {
                settledCount++;
            }
        }

        var order = await ReadAsync(orderId);

        Assert.Equal(1, settledCount);
        Assert.Equal(OrderStatus.Completed, order.Status);

        // The timestamp is the real assertion. A status check alone would pass even if every
        // delivery rewrote the row, because it would keep rewriting it to the same status.
        Assert.Equal(completedAt, order.UpdatedAt, TimeSpan.FromMilliseconds(1));
    }

    // ---------------------------------------------------------------- User Story 2

    [Fact]
    public async Task OrderFailedEvent_settles_a_submitted_order_and_records_the_reason()
    {
        var orderId = await SeedSubmittedOrderAsync();
        var failedAt = DateTime.UtcNow;

        await _fixture.Harness.Bus.Publish(
            new OrderFailedEvent(orderId, "Payment rejected by the configured outcome", failedAt));

        var order = await WaitForSettlementAsync(orderId);

        Assert.Equal(OrderStatus.Failed, order.Status);
        Assert.Equal("Payment rejected by the configured outcome", order.FailureReason);
    }

    [Fact]
    public async Task A_repeated_failure_notice_leaves_the_original_reason_untouched()
    {
        var orderId = await SeedSubmittedOrderAsync();
        var failedAt = new DateTime(2026, 9, 16, 11, 0, 0, DateTimeKind.Utc);

        Assert.True(await SendAsync(new FailOrderCommand(orderId, "Insufficient stock", failedAt)));

        // A second delivery carrying a different explanation. If the guard were missing, this would
        // overwrite a settled order's reason with a later one — and nothing would look wrong.
        Assert.False(await SendAsync(
            new FailOrderCommand(orderId, "Something else entirely", failedAt.AddMinutes(5))));

        var order = await ReadAsync(orderId);

        Assert.Equal("Insufficient stock", order.FailureReason);
        Assert.Equal(failedAt, order.UpdatedAt, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task A_failure_notice_cannot_unsettle_a_completed_order()
    {
        var orderId = await SeedSubmittedOrderAsync();

        Assert.True(await SendAsync(new CompleteOrderCommand(orderId, DateTime.UtcNow)));
        Assert.False(await SendAsync(
            new FailOrderCommand(orderId, "Arrived late and out of order", DateTime.UtcNow)));

        var order = await ReadAsync(orderId);

        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Null(order.FailureReason);
    }

    [Fact]
    public async Task A_notice_for_an_order_this_service_does_not_hold_is_discarded()
    {
        var strangerId = Guid.CreateVersion7();

        // No exception, and no claim to have settled anything. Throwing here would make the broker
        // redeliver a message about an order that is never going to appear.
        Assert.False(await SendAsync(new CompleteOrderCommand(strangerId, DateTime.UtcNow)));
        Assert.False(await SendAsync(new FailOrderCommand(strangerId, "whatever", DateTime.UtcNow)));
    }

    [Fact]
    public async Task An_overlong_reason_is_truncated_rather_than_failing_the_settlement()
    {
        var orderId = await SeedSubmittedOrderAsync();

        // FailureReason is varchar(512); Reason on the contract is unbounded.
        var reason = new string('x', 900);

        Assert.True(await SendAsync(new FailOrderCommand(orderId, reason, DateTime.UtcNow)));

        var order = await ReadAsync(orderId);

        Assert.Equal(OrderStatus.Failed, order.Status);
        Assert.Equal(512, order.FailureReason!.Length);
    }

    // ---------------------------------------------------------------- helpers

    private async Task<Guid> SeedSubmittedOrderAsync(Guid? userId = null)
    {
        var orderId = Guid.CreateVersion7();

        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        // Backdated so that "UpdatedAt moved" is a meaningful assertion rather than a race with the
        // clock's resolution.
        var createdAt = DateTime.UtcNow.AddMinutes(-5);

        context.Orders.Add(new Domain.Entities.Order
        {
            Id = orderId,
            UserId = userId ?? Guid.CreateVersion7(),
            TotalAmount = 129.99m,
            Status = OrderStatus.Submitted,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            Items =
            [
                new OrderItem
                {
                    Id = Guid.CreateVersion7(),
                    OrderId = orderId,
                    ProductId = Guid.CreateVersion7(),
                    ProductName = "Mechanical Keyboard",
                    Quantity = 1,
                    UnitPrice = 129.99m
                }
            ]
        });

        await context.SaveChangesAsync();

        return orderId;
    }

    /// <summary>
    /// Polls the order row until it leaves <see cref="OrderStatus.Submitted"/>.
    /// </summary>
    /// <remarks>
    /// Deliberately not <c>Harness.Consumed.Any&lt;T&gt;()</c>. That waits on the harness's
    /// inactivity token, which is scoped to a harness created per test — with a fixture shared
    /// across a collection the token has usually elapsed by the time the later tests run, and the
    /// call then returns <c>false</c> immediately for a message that was consumed perfectly well.
    /// It cost two red tests that passed in isolation.
    /// <para>
    /// Asserting on the row is also the better assertion: it is the outcome the feature promises,
    /// rather than the harness's record of having seen a message.
    /// </para>
    /// </remarks>
    private async Task<Domain.Entities.Order> WaitForSettlementAsync(Guid orderId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);

        while (DateTime.UtcNow < deadline)
        {
            var order = await ReadAsync(orderId);

            if (order.Status != OrderStatus.Submitted)
            {
                return order;
            }

            await Task.Delay(100);
        }

        throw new Xunit.Sdk.XunitException(
            $"Order {orderId} was still Submitted 15 seconds after the notice was published. "
            + "The consumer is probably not registered on a receive endpoint.");
    }

    private async Task<Domain.Entities.Order> ReadAsync(Guid orderId)
    {
        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        return await context.Orders.AsNoTracking().SingleAsync(x => x.Id == orderId);
    }

    private async Task<TResult> SendAsync<TResult>(IRequest<TResult> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
