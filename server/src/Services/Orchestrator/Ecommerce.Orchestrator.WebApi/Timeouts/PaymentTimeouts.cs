using Ecommerce.Orchestrator.WebApi.StateMachines;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Orchestrator.WebApi.Timeouts;

/// <summary>
/// Which orders have waited too long for Payment, and telling the saga so (specs/053).
/// </summary>
public static class PaymentTimeouts
{
    /// <summary>
    /// The state an order waits for Payment in. Its NAME, because that is what the saga table stores -
    /// <c>OrchestratorTests.The_state_the_sweeper_looks_for_is_the_one_the_saga_stores</c> holds the two
    /// together.
    /// </summary>
    public const string AwaitingPayment = nameof(OrderStateMachine.InventoryReservedState);

    /// <summary>At most this many per sweep, so one backlog cannot make one tick unbounded.</summary>
    public const int BatchSize = 200;

    /// <summary>Orders still waiting for Payment that entered the wait before <paramref name="cutoff"/>.</summary>
    /// <remarks>
    /// <c>UpdatedAt</c> is set on entering the wait - the moment Inventory's hold starts counting too - and
    /// nothing in that state writes it again.
    /// </remarks>
    public static Task<List<Guid>> DueAsync(OrchestratorDbContext context, DateTime cutoff, CancellationToken ct = default) =>
        context.Set<OrderStateData>()
            .AsNoTracking()
            .Where(s => s.CurrentState == AwaitingPayment && s.UpdatedAt < cutoff)
            .OrderBy(s => s.UpdatedAt)
            .Select(s => s.CorrelationId)
            .Take(BatchSize)
            .ToListAsync(ct);

    /// <summary>
    /// Tells the saga about each due order, through the outbox: the messages commit with the one save or
    /// not at all (Principle III), and a saga instance handles a repeat as nothing.
    /// </summary>
    public static async Task<int> ExpireAsync(
        OrchestratorDbContext context, IPublishEndpoint publish, DateTime cutoff, CancellationToken ct = default)
    {
        var due = await DueAsync(context, cutoff, ct);
        foreach (var orderId in due)
        {
            await publish.Publish(new PaymentTimeoutExpired(orderId), ct);
        }

        await context.SaveChangesAsync(ct);
        return due.Count;
    }
}
