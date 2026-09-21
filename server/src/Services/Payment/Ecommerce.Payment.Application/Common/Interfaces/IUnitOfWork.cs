namespace Ecommerce.Payment.Application.Common.Interfaces;

public interface IUnitOfWork
{
    /// <summary>
    /// Runs the operation inside one database transaction and commits it, so the payment row and
    /// the outbox entry carrying its reply commit together or not at all.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets everything staged but not committed, so the unit of work can be reused after a
    /// failed save.
    /// </summary>
    /// <remarks>
    /// A failed <c>SaveChangesAsync</c> does <b>not</b> untrack what it tried to write. The rows are
    /// still pending, so the next save re-attempts them — and if the first save failed on a unique
    /// constraint, the second fails on the same one, outside whatever caught the first.
    /// <para>
    /// That is not hypothetical: it is how
    /// <c>Fifty_simultaneous_requests_record_exactly_one_payment</c> broke CI on 2026-09-19, from a
    /// tree byte-identical to one that had passed minutes earlier on a pull request.
    /// </para>
    /// </remarks>
    void DiscardPendingChanges();
}
