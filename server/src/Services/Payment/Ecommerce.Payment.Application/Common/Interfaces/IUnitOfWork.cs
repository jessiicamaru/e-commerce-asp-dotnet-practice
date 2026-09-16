namespace Ecommerce.Payment.Application.Common.Interfaces;

public interface IUnitOfWork
{
    /// <summary>
    /// Runs the operation inside one database transaction and commits it, so the payment row and
    /// the outbox entry carrying its reply commit together or not at all.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
}
