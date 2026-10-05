using MassTransit;

namespace Ecommerce.Shared.Messaging;

/// <summary>
/// A consumer's transaction lost to a transient database failure is tried again, in a new transaction (specs/145,
/// #299).
/// <para>
/// MassTransit's EF outbox consumes in a <c>RepeatableRead</c> transaction, and PostgreSQL aborts the losers of
/// concurrent updates to one row with a serialization failure (<c>40001</c>). That is safe only if the message is tried
/// again; without a retry it faulted to its <c>_error</c> queue. That is how the load test of #290 found a paid order
/// whose stock confirmation was lost, leaving the units held for the expiry sweeper to put back on the shelf.
/// </para>
/// </summary>
public static class TransientRetry
{
    private const string SerializationFailure = "40001";
    private const string DeadlockDetected = "40P01";
    private const string UniqueViolation = "23505";

    /// <summary>
    /// The inbox's own key (MassTransit's <c>AddTransactionalOutboxEntities</c>, the same name in every service). Two
    /// deliveries of one message consumed at once both insert it, and the second fails on it (specs/149, #306); tried
    /// again, it finds the row consumed and the inbox drops the duplicate.
    /// </summary>
    private const string InboxKey = "AK_InboxState_MessageId_ConsumerId";

    /// <summary>
    /// Whether anything in the chain is a failure worth trying again: PostgreSQL's serialization failure or deadlock, a
    /// duplicate on the inbox's key, or an Npgsql exception that marks itself transient (a lost connection). Read by
    /// property name, as Payment does, so this building block takes no dependency on Npgsql. Any other unique violation
    /// is a decision about the shop's own data, never retried.
    /// </summary>
    public static bool IsTransient(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var type = current.GetType();
            var sqlState = type.GetProperty("SqlState")?.GetValue(current) as string;
            if (sqlState is SerializationFailure or DeadlockDetected)
            {
                return true;
            }

            if (sqlState == UniqueViolation && type.GetProperty("ConstraintName")?.GetValue(current) as string == InboxKey)
            {
                return true;
            }

            if (type.FullName?.StartsWith("Npgsql.", StringComparison.Ordinal) == true
                && type.GetProperty("IsTransient")?.GetValue(current) is true)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The retry every receive endpoint gets. Call it BEFORE <c>UseEntityFrameworkOutbox</c>, so it wraps the outbox:
    /// each attempt is a fresh transaction, inbox check and outbox, and a failed attempt's messages are discarded with
    /// it. Only transient failures are retried - a bug faults at once, as before.
    /// </summary>
    public static void UseTransientRetry(this IConsumePipeConfigurator configurator)
    {
        configurator.UseMessageRetry(retry =>
        {
            retry.Handle<Exception>(IsTransient);
            // Growing, jittered intervals: the losers collided because they ran together, so they must not again.
            retry.Exponential(10, TimeSpan.FromMilliseconds(20), TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(50));
        });
    }
}
