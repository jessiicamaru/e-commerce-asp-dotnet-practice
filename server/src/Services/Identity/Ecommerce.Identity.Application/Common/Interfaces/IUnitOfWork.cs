namespace Ecommerce.Application.Common.Interfaces;

public interface IUnitOfWork
{
    /// <summary>
    /// Runs the operation in one database transaction and commits it.
    /// </summary>
    /// <remarks>
    /// Required wherever a row lock is taken: SELECT ... FOR UPDATE holds its lock only for the life of
    /// a transaction, and EF opens a fresh one per SaveChangesAsync, so without this the lock would be
    /// released before the change it protects was written.
    /// </remarks>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
}
