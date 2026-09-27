using Ecommerce.Contracts.Inventory;
using Ecommerce.Contracts.Order;
using Ecommerce.Contracts.Payment;
using Ecommerce.Orchestrator.WebApi.StateMachines;
using Ecommerce.Orchestrator.WebApi.Timeouts;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Orchestrator.Tests;

/// <summary>
/// The checkout saga, one transition at a time (specs/053) - the first tests it has had. Until now its
/// behaviour was proved only end to end by verify-saga.sh, which can reach neither a timeout nor a payment
/// that answers late.
/// </summary>
/// <remarks>
/// MassTransit's harness with an in-memory saga repository: what is under test is which messages each
/// transition publishes and where the instance ends up, not persistence - the sweeper's query has its own
/// tests against PostgreSQL.
/// </remarks>
public sealed class OrderStateMachineTests : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;
    private ISagaStateMachineTestHarness<OrderStateMachine, OrderStateData> _saga = null!;

    public async Task InitializeAsync()
    {
        _provider = new ServiceCollection()
            .AddMassTransitTestHarness(x => x.AddSagaStateMachine<OrderStateMachine, OrderStateData>().InMemoryRepository())
            .BuildServiceProvider(true);
        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();
        _saga = _harness.GetSagaStateMachineHarness<OrderStateMachine, OrderStateData>();
    }

    public async Task DisposeAsync() => await _provider.DisposeAsync();

    [Fact]
    public async Task A_paid_order_completes()
    {
        var order = await ReservedAsync();

        await _harness.Bus.Publish(new PaymentProcessedEvent(order, Guid.NewGuid(), DateTime.UtcNow));

        Assert.True(await _harness.Published.Any<OrderCompletedEvent>(m => m.Context.Message.OrderId == order));
        Assert.Null(await _saga.NotExists(order)); // null: the instance is gone (finalized)
    }

    /// <summary>
    /// #186 (specs/094): the saga relays what Order froze - the currency into the payment (specs/022 SC-004, until now
    /// only half automated) and each line's variant into the reservation (the specs/020 relay gotcha: a saga built
    /// against an older contract dropped VariantId and the wrong variant's stock moved).
    /// </summary>
    [Fact]
    public async Task The_saga_relays_the_currency_to_Payment_and_the_variant_to_Inventory()
    {
        var order = Guid.NewGuid();
        var customer = Guid.NewGuid();
        var variant = Guid.NewGuid();

        await _harness.Bus.Publish(new OrderSubmittedEvent(order, customer, 49.99m,
            [new OrderItemDto(Guid.NewGuid(), 2, 24.995m, variant)], DateTime.UtcNow, "USD"));
        Assert.NotNull(await _saga.Exists(order, m => m.Submitted));
        var reserve = Assert.Single(PublishedFor<ReserveInventoryCommand>(order));
        Assert.Equal((variant, 2), (Assert.Single(reserve.Items).VariantId, reserve.Items[0].Quantity));

        await _harness.Bus.Publish(new InventoryReservedEvent(order, DateTime.UtcNow));
        Assert.NotNull(await _saga.Exists(order, m => m.InventoryReservedState));
        Assert.True(await _harness.Published.Any<ProcessPaymentCommand>(m => m.Context.Message.OrderId == order));

        var pay = Assert.Single(PublishedFor<ProcessPaymentCommand>(order));
        Assert.Equal((customer, 49.99m, "USD"), (pay.UserId, pay.Amount, pay.Currency));
    }

    [Fact]
    public async Task A_rejected_payment_releases_the_stock_and_fails_the_order()
    {
        var order = await ReservedAsync();

        await _harness.Bus.Publish(new PaymentFailedEvent(order, "Card declined"));

        Assert.True(await _harness.Published.Any<ReleaseInventoryCommand>(m => m.Context.Message.OrderId == order));
        Assert.True(await _harness.Published.Any<OrderFailedEvent>(m => m.Context.Message.OrderId == order));
        Assert.Null(await _saga.NotExists(order)); // null: the instance is gone (finalized)
    }

    /// <summary>#123: waiting for Payment had no end, and Inventory's hold does.</summary>
    [Fact]
    public async Task An_unanswered_payment_releases_the_stock_fails_the_order_and_waits_for_a_late_answer()
    {
        var order = await ReservedAsync();

        await _harness.Bus.Publish(new PaymentTimeoutExpired(order));

        Assert.NotNull(await _saga.Exists(order, m => m.PaymentTimedOut));
        Assert.True(await _harness.Published.Any<ReleaseInventoryCommand>(m => m.Context.Message.OrderId == order));
        var failed = Assert.Single(PublishedFor<OrderFailedEvent>(order));
        Assert.Contains("did not answer", failed.Reason);
        Assert.Empty(PublishedFor<OrderCompletedEvent>(order));
    }

    /// <summary>The money was taken for stock that is back on the shelf: give it back, and nothing else.</summary>
    [Fact]
    public async Task A_payment_approved_after_the_timeout_is_refunded_and_the_order_stays_failed()
    {
        var order = await TimedOutAsync();

        await _harness.Bus.Publish(new PaymentProcessedEvent(order, Guid.NewGuid(), DateTime.UtcNow));

        Assert.True(await _harness.Published.Any<RefundPaymentCommand>(m => m.Context.Message.OrderId == order));
        Assert.Null(await _saga.NotExists(order)); // null: the instance is gone (finalized)
        Assert.Empty(PublishedFor<OrderCompletedEvent>(order));
        Assert.Single(PublishedFor<OrderFailedEvent>(order));
    }

    [Fact]
    public async Task A_payment_rejected_after_the_timeout_needs_nothing()
    {
        var order = await TimedOutAsync();

        await _harness.Bus.Publish(new PaymentFailedEvent(order, "Card declined"));

        Assert.Null(await _saga.NotExists(order)); // null: the instance is gone (finalized)
        Assert.Empty(PublishedFor<RefundPaymentCommand>(order));
        Assert.Single(PublishedFor<OrderFailedEvent>(order));
        Assert.Single(PublishedFor<ReleaseInventoryCommand>(order));
    }

    /// <summary>The race the other way: Payment answered first, and the timeout finds nothing to do.</summary>
    [Fact]
    public async Task A_timeout_after_the_order_completed_changes_nothing()
    {
        var order = await ReservedAsync();
        await _harness.Bus.Publish(new PaymentProcessedEvent(order, Guid.NewGuid(), DateTime.UtcNow));
        Assert.Null(await _saga.NotExists(order)); // null: the instance is gone (finalized)

        await _harness.Bus.Publish(new PaymentTimeoutExpired(order));

        Assert.True(await _harness.Consumed.Any<PaymentTimeoutExpired>(m => m.Context.Message.OrderId == order));
        Assert.Empty(PublishedFor<ReleaseInventoryCommand>(order));
        Assert.Empty(PublishedFor<OrderFailedEvent>(order));
    }

    /// <summary>Two sweepers, or two ticks, announcing one order: it fails once.</summary>
    [Fact]
    public async Task A_repeated_timeout_fails_the_order_once()
    {
        var order = await TimedOutAsync();

        await _harness.Bus.Publish(new PaymentTimeoutExpired(order));

        Assert.True(await _harness.Consumed.Any<PaymentTimeoutExpired>(m => m.Context.Message.OrderId == order && m.Context.Message == new PaymentTimeoutExpired(order)));
        await Task.Delay(200);
        // Handled as nothing - not refused: an unhandled event in a state faults, and a fault per duplicate
        // would fill the error queue with orders that are fine.
        Assert.False(await _harness.Published.Any<Fault<PaymentTimeoutExpired>>());
        Assert.Single(PublishedFor<ReleaseInventoryCommand>(order));
        Assert.Single(PublishedFor<OrderFailedEvent>(order));
        Assert.NotNull(await _saga.Exists(order, m => m.PaymentTimedOut));
    }

    /// <summary>The sweeper finds waiting orders by the state's NAME in the saga table; the two must agree.</summary>
    [Fact]
    public async Task The_state_the_sweeper_looks_for_is_the_one_the_saga_stores()
    {
        var order = await ReservedAsync();

        var instance = _saga.Sagas.Contains(order);
        Assert.NotNull(instance);
        Assert.Equal(PaymentTimeouts.AwaitingPayment, instance.CurrentState);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<Guid> ReservedAsync()
    {
        var order = Guid.NewGuid();
        await _harness.Bus.Publish(new OrderSubmittedEvent(order, Guid.NewGuid(), 125_000m,
            [new OrderItemDto(Guid.NewGuid(), 1, 125_000m)], DateTime.UtcNow, "VND"));
        Assert.NotNull(await _saga.Exists(order, m => m.Submitted));

        await _harness.Bus.Publish(new InventoryReservedEvent(order, DateTime.UtcNow));
        Assert.NotNull(await _saga.Exists(order, m => m.InventoryReservedState));
        return order;
    }

    private async Task<Guid> TimedOutAsync()
    {
        var order = await ReservedAsync();
        await _harness.Bus.Publish(new PaymentTimeoutExpired(order));
        Assert.NotNull(await _saga.Exists(order, m => m.PaymentTimedOut));
        return order;
    }

    private List<T> PublishedFor<T>(Guid order) where T : class =>
        _harness.Published.Select<T>()
            .Select(m => m.Context.Message)
            .Where(m => (Guid)typeof(T).GetProperty("OrderId")!.GetValue(m)! == order)
            .ToList();
}
