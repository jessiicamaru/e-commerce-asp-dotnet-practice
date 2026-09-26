using Ecommerce.Catalog.Domain.Entities;

namespace Ecommerce.Catalog.Application.Common.Interfaces;

/// <summary>A question with the product it is about, for a queue (specs/076).</summary>
public record QuestionRow(ProductQuestion Question, string ProductName);

/// <summary>
/// Questions and their answers (specs/076). Every change after the first insert is ONE guarded statement: only if it
/// changed a row does <c>stage</c> run (the audit entry, the notice) and the save happen - in its transaction, so
/// a decision and what announces it commit together or not at all, and of two people at once exactly one decides.
/// </summary>
public interface IProductQuestionRepository
{
    Task<ProductQuestion?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>A product's questions that are not hidden, newest first.</summary>
    Task<(List<ProductQuestion> Items, int TotalCount)> GetVisibleAsync(Guid productId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// The visible questions on one seller's products - or, for <paramref name="sellerId"/> null, on the shop's own -
    /// unanswered or answered, oldest unanswered first so nobody waits longest; answered ones newest first.
    /// </summary>
    Task<(List<QuestionRow> Items, int TotalCount)> GetQueueAsync(Guid? sellerId, bool answered, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>For moderators: the visible questions, or those with something hidden - the question or its answer.</summary>
    Task<(List<QuestionRow> Items, int TotalCount)> GetForStaffAsync(bool hidden, int page, int pageSize, CancellationToken cancellationToken = default);

    Task AddAsync(ProductQuestion question, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>The first answer: only while nothing answers it and the question is not hidden.</summary>
    Task<int> TryAnswerFirstAsync(Guid id, string answer, Guid by, DateTime now, Func<CancellationToken, Task> stage, CancellationToken cancellationToken = default);

    /// <summary>A rewrite: only of an answer that exists, on a question that is not hidden, and that staff have not hidden.</summary>
    Task<int> TryRewriteAsync(Guid id, string answer, Guid by, DateTime now, Func<CancellationToken, Task> stage, CancellationToken cancellationToken = default);

    Task<int> TryHideAsync(Guid id, string reason, Guid by, DateTime now, Func<CancellationToken, Task> stage, CancellationToken cancellationToken = default);

    Task<int> TryRestoreAsync(Guid id, Func<CancellationToken, Task> stage, CancellationToken cancellationToken = default);

    Task<int> TryHideAnswerAsync(Guid id, string reason, Guid by, DateTime now, Func<CancellationToken, Task> stage, CancellationToken cancellationToken = default);

    Task<int> TryRestoreAnswerAsync(Guid id, Func<CancellationToken, Task> stage, CancellationToken cancellationToken = default);
}
