namespace Ecommerce.Inventory.Application.Common.Interfaces;

public interface IUnitOfWork
{
    /// <summary>
    /// Runs the operation inside one database transaction and commits it.
    /// <para>
    /// Required wherever a row lock is taken: <c>SELECT ... FOR UPDATE</c> holds its lock only for
    /// the life of a transaction, and EF opens a fresh one per <c>SaveChangesAsync</c>, so without
    /// this the lock would be released before the decision based on it was written.
    /// </para>
    /// The operation is responsible for staging its changes, publishing its messages, and calling
    /// <c>SaveChangesAsync</c> — in that order, so the rows and the outbox entry commit together.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
}
