using Ecommerce.Catalog.Application.Common;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Common.Models;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Notifications;
using FluentValidation;
using MediatR;

namespace Ecommerce.Catalog.Application.Questions;

/// <summary>
/// A question and its answer (specs/076). What a shopper reads carries no hidden answer and no reasons; a queue
/// and the moderators' views carry everything, plus the product's name.
/// </summary>
public record QuestionResponse(
    Guid Id,
    Guid ProductId,
    string AskerName,
    string Body,
    DateTime CreatedAt,
    string? Answer,
    DateTime? AnsweredAt,
    bool AnswerEdited,
    string? ProductName = null,
    DateTime? HiddenAt = null,
    string? HiddenReason = null,
    DateTime? AnswerHiddenAt = null,
    string? AnswerHiddenReason = null)
{
    /// <summary>As the public reads it: a hidden answer is no answer.</summary>
    public static QuestionResponse Public(ProductQuestion q) => q.AnswerHiddenAt is null
        ? new(q.Id, q.ProductId, q.AskerName, q.Body, q.CreatedAt, q.Answer, q.AnsweredAt, Edited(q))
        : new(q.Id, q.ProductId, q.AskerName, q.Body, q.CreatedAt, null, null, false);

    /// <summary>As somebody who answers or moderates reads it: all of it.</summary>
    public static QuestionResponse Full(ProductQuestion q, string? productName) => new(
        q.Id, q.ProductId, q.AskerName, q.Body, q.CreatedAt, q.Answer, q.AnsweredAt, Edited(q), productName,
        q.HiddenAt, q.HiddenReason, q.AnswerHiddenAt, q.AnswerHiddenReason);

    private static bool Edited(ProductQuestion q) =>
        q.AnsweredAt is { } first && q.AnswerUpdatedAt is { } last && last > first.AddSeconds(1);
}

public record GetProductQuestionsQuery(Guid ProductId, int PageNumber = 1, int PageSize = 12) : IRequest<PaginatedList<QuestionResponse>>;

public record AskQuestionCommand(Guid ProductId, string Body) : IRequest<QuestionResponse>;

/// <summary>Answers a question, or rewrites the answer: one per question.</summary>
public record AnswerQuestionCommand(Guid QuestionId, string Answer) : IRequest<QuestionResponse>;

/// <summary>What the caller answers for: a seller's own products, or for staff the shop's own.</summary>
public record GetQuestionsToAnswerQuery(bool Answered = false, int PageNumber = 1, int PageSize = 12) : IRequest<PaginatedList<QuestionResponse>>;

public record GetQuestionsForStaffQuery(bool Hidden = false, int PageNumber = 1, int PageSize = 12) : IRequest<PaginatedList<QuestionResponse>>;

public record HideQuestionCommand(Guid QuestionId, string Reason) : IRequest<QuestionResponse>;

public record RestoreQuestionCommand(Guid QuestionId) : IRequest<QuestionResponse>;

public record HideAnswerCommand(Guid QuestionId, string Reason) : IRequest<QuestionResponse>;

public record RestoreAnswerCommand(Guid QuestionId) : IRequest<QuestionResponse>;

public class AskQuestionCommandValidator : AbstractValidator<AskQuestionCommand>
{
    public AskQuestionCommandValidator() =>
        RuleFor(x => x.Body).Must(b => !string.IsNullOrWhiteSpace(b)).WithMessage("Question is required.").MaximumLength(1000);
}

public class AnswerQuestionCommandValidator : AbstractValidator<AnswerQuestionCommand>
{
    public AnswerQuestionCommandValidator() =>
        RuleFor(x => x.Answer).Must(a => !string.IsNullOrWhiteSpace(a)).WithMessage("Answer is required.").MaximumLength(2000);
}

public class HideQuestionCommandValidator : AbstractValidator<HideQuestionCommand>
{
    public HideQuestionCommandValidator() =>
        RuleFor(x => x.Reason).Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("Reason is required.").MaximumLength(500);
}

public class HideAnswerCommandValidator : AbstractValidator<HideAnswerCommand>
{
    public HideAnswerCommandValidator() =>
        RuleFor(x => x.Reason).Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("Reason is required.").MaximumLength(500);
}

public class QuestionPagingValidators : AbstractValidator<GetProductQuestionsQuery>
{
    public QuestionPagingValidators()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}

public class QuestionHandlers(
    IProductQuestionRepository questions,
    IProductRepository products,
    ICurrentUser currentUser,
    IAuditTrail audit,
    INotifier notifier) :
    IRequestHandler<GetProductQuestionsQuery, PaginatedList<QuestionResponse>>,
    IRequestHandler<AskQuestionCommand, QuestionResponse>,
    IRequestHandler<AnswerQuestionCommand, QuestionResponse>,
    IRequestHandler<GetQuestionsToAnswerQuery, PaginatedList<QuestionResponse>>,
    IRequestHandler<GetQuestionsForStaffQuery, PaginatedList<QuestionResponse>>,
    IRequestHandler<HideQuestionCommand, QuestionResponse>,
    IRequestHandler<RestoreQuestionCommand, QuestionResponse>,
    IRequestHandler<HideAnswerCommand, QuestionResponse>,
    IRequestHandler<RestoreAnswerCommand, QuestionResponse>
{
    public const string NotFound = "Question not found.";
    public const string OwnProduct = "You cannot ask about your own product.";
    public const string Locked = "This question or its answer was hidden by a moderator.";

    private readonly IProductQuestionRepository _questions = questions;
    private readonly IProductRepository _products = products;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditTrail _audit = audit;
    private readonly INotifier _notifier = notifier;

    public async Task<PaginatedList<QuestionResponse>> Handle(GetProductQuestionsQuery request, CancellationToken cancellationToken)
    {
        // The public lookup's rule (specs/045, #166): off the shelf, only its seller and staff read its questions.
        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null || !ProductReview.MaySee(product, _currentUser))
            throw new NotFoundException("Product not found.");

        var (items, total) = await _questions.GetVisibleAsync(request.ProductId, request.PageNumber, request.PageSize, cancellationToken);
        return new PaginatedList<QuestionResponse>(items.Select(QuestionResponse.Public).ToList(), total, request.PageNumber, request.PageSize);
    }

    public async Task<QuestionResponse> Handle(AskQuestionCommand request, CancellationToken cancellationToken)
    {
        var me = Caller();
        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken);

        // The public lookup's rule (specs/045): not on sale is the same 404 as not there.
        if (product is null || !product.IsListed || !product.IsActive)
            throw new NotFoundException("Product not found.");

        // A seller asking their own shop something is a note to themselves on a public page.
        if (product.SellerId == me)
            throw new ForbiddenException(OwnProduct);

        var question = new ProductQuestion
        {
            ProductId = product.Id,
            AskerId = me,
            AskerName = AskerName(),
            Body = request.Body.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
        await _questions.AddAsync(question, cancellationToken);

        await _audit.RecordAsync(AuditCategory.Catalog, "QuestionAsked", "Question", question.Id.ToString(),
            $"A question about \"{product.Name}\"", after: new { question.Body }, cancellationToken: cancellationToken);

        // The seller hears about it; the shop's own products have nobody in particular - staff read their queue.
        if (product.SellerId is { } seller)
        {
            await _notifier.NotifyAsync(seller, NotificationKind.NewQuestion,
                new Dictionary<string, string> { ["product"] = product.Name }, "/shop/questions", cancellationToken);
        }

        await _questions.SaveChangesAsync(cancellationToken);
        return QuestionResponse.Public(question);
    }

    public async Task<QuestionResponse> Handle(AnswerQuestionCommand request, CancellationToken cancellationToken)
    {
        var me = Caller();
        var (question, product) = await AnswerableAsync(request.QuestionId, me, cancellationToken);
        var answer = request.Answer.Trim();
        var now = DateTime.UtcNow;

        // The first answer tells the asker; two first answers at once - one of them becomes the rewrite below.
        var first = await _questions.TryAnswerFirstAsync(question.Id, answer, me, now, async ct =>
        {
            await _audit.RecordAsync(AuditCategory.Catalog, "QuestionAnswered", "Question", question.Id.ToString(),
                $"A question about \"{product.Name}\" answered", after: new { Answer = answer }, cancellationToken: ct);

            if (question.AskerId != me)
            {
                await _notifier.NotifyAsync(question.AskerId, NotificationKind.QuestionAnswered,
                    new Dictionary<string, string> { ["product"] = product.Name }, $"/products/{product.Id}", ct);
            }
        }, cancellationToken);

        if (first == 0)
        {
            var before = question.Answer;
            var rewritten = await _questions.TryRewriteAsync(question.Id, answer, me, now, ct =>
                _audit.RecordAsync(AuditCategory.Catalog, "AnswerEdited", "Question", question.Id.ToString(),
                    $"The answer about \"{product.Name}\" rewritten", new { Answer = before }, new { Answer = answer },
                    cancellationToken: ct), cancellationToken);

            // Neither applied: staff hid the question or the answer. A rewrite does not undo a moderator (research D3).
            if (rewritten == 0)
                throw new ConflictException(Locked);
        }

        return QuestionResponse.Full(await ReloadAsync(question.Id, cancellationToken), product.Name);
    }

    public async Task<PaginatedList<QuestionResponse>> Handle(GetQuestionsToAnswerQuery request, CancellationToken cancellationToken)
    {
        var me = Caller();
        // Staff answer for the shop's own products; a seller for theirs. Nobody gets somebody else's queue.
        Guid? sellerId = IsStaff() ? null : me;
        var (items, total) = await _questions.GetQueueAsync(sellerId, request.Answered, request.PageNumber, request.PageSize, cancellationToken);
        return new PaginatedList<QuestionResponse>(
            items.Select(r => QuestionResponse.Full(r.Question, r.ProductName)).ToList(), total, request.PageNumber, request.PageSize);
    }

    public async Task<PaginatedList<QuestionResponse>> Handle(GetQuestionsForStaffQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _questions.GetForStaffAsync(request.Hidden, request.PageNumber, request.PageSize, cancellationToken);
        return new PaginatedList<QuestionResponse>(
            items.Select(r => QuestionResponse.Full(r.Question, r.ProductName)).ToList(), total, request.PageNumber, request.PageSize);
    }

    /// <summary>Hidden, not deleted: it leaves the page with its answer, and a moderator can put it back.</summary>
    public async Task<QuestionResponse> Handle(HideQuestionCommand request, CancellationToken cancellationToken)
    {
        var question = await _questions.GetAsync(request.QuestionId, cancellationToken) ?? throw new NotFoundException(NotFound);
        var product = await _products.GetByIdAsync(question.ProductId, cancellationToken);
        var reason = request.Reason.Trim();

        var hidden = await _questions.TryHideAsync(question.Id, reason, Caller(), DateTime.UtcNow, async ct =>
        {
            await _audit.RecordAsync(AuditCategory.Moderation, "QuestionHidden", "Question", question.Id.ToString(),
                $"A question hidden: {reason}", new { Hidden = false }, new { Hidden = true, Reason = reason }, cancellationToken: ct);

            // Its asker learns why it disappeared, as a reviewer does (specs/059).
            await _notifier.NotifyAsync(question.AskerId, NotificationKind.QuestionHidden,
                new Dictionary<string, string> { ["product"] = product?.Name ?? "", ["reason"] = reason },
                $"/products/{question.ProductId}", ct);
        }, cancellationToken);
        if (hidden == 0)
            throw new ConflictException("This question is already hidden.");

        return QuestionResponse.Full(await ReloadAsync(question.Id, cancellationToken), product?.Name);
    }

    public async Task<QuestionResponse> Handle(RestoreQuestionCommand request, CancellationToken cancellationToken)
    {
        var question = await _questions.GetAsync(request.QuestionId, cancellationToken) ?? throw new NotFoundException(NotFound);
        var product = await _products.GetByIdAsync(question.ProductId, cancellationToken);
        var reason = question.HiddenReason;

        var restored = await _questions.TryRestoreAsync(question.Id, ct =>
            _audit.RecordAsync(AuditCategory.Moderation, "QuestionRestored", "Question", question.Id.ToString(),
                "A question shown again", new { Hidden = true, Reason = reason }, new { Hidden = false }, cancellationToken: ct),
            cancellationToken);
        if (restored == 0)
            throw new ConflictException("This question is not hidden.");

        return QuestionResponse.Full(await ReloadAsync(question.Id, cancellationToken), product?.Name);
    }

    /// <summary>The question stays and reads as unanswered; the answer is locked until restored.</summary>
    public async Task<QuestionResponse> Handle(HideAnswerCommand request, CancellationToken cancellationToken)
    {
        var question = await _questions.GetAsync(request.QuestionId, cancellationToken) ?? throw new NotFoundException(NotFound);
        var product = await _products.GetByIdAsync(question.ProductId, cancellationToken);
        var reason = request.Reason.Trim();

        var hidden = await _questions.TryHideAnswerAsync(question.Id, reason, Caller(), DateTime.UtcNow, async ct =>
        {
            await _audit.RecordAsync(AuditCategory.Moderation, "AnswerHidden", "Question", question.Id.ToString(),
                $"An answer hidden: {reason}", new { AnswerHidden = false }, new { AnswerHidden = true, Reason = reason },
                cancellationToken: ct);

            if (question.AnsweredBy is { } author)
            {
                await _notifier.NotifyAsync(author, NotificationKind.AnswerHidden,
                    new Dictionary<string, string> { ["product"] = product?.Name ?? "", ["reason"] = reason },
                    $"/products/{question.ProductId}", ct);
            }
        }, cancellationToken);
        if (hidden == 0)
            throw new ConflictException("This question has no visible answer to hide.");

        return QuestionResponse.Full(await ReloadAsync(question.Id, cancellationToken), product?.Name);
    }

    public async Task<QuestionResponse> Handle(RestoreAnswerCommand request, CancellationToken cancellationToken)
    {
        var question = await _questions.GetAsync(request.QuestionId, cancellationToken) ?? throw new NotFoundException(NotFound);
        var product = await _products.GetByIdAsync(question.ProductId, cancellationToken);
        var reason = question.AnswerHiddenReason;

        var restored = await _questions.TryRestoreAnswerAsync(question.Id, ct =>
            _audit.RecordAsync(AuditCategory.Moderation, "AnswerRestored", "Question", question.Id.ToString(),
                "An answer shown again", new { AnswerHidden = true, Reason = reason }, new { AnswerHidden = false },
                cancellationToken: ct), cancellationToken);
        if (restored == 0)
            throw new ConflictException("This answer is not hidden.");

        return QuestionResponse.Full(await ReloadAsync(question.Id, cancellationToken), product?.Name);
    }

    /// <summary>
    /// Who answers (research D2): a seller's product, only that seller; the shop's own, only staff. Anybody else - an
    /// administrator on a seller's product included - gets the one 404 that "not there" gets too.
    /// </summary>
    private async Task<(ProductQuestion Question, Product Product)> AnswerableAsync(Guid questionId, Guid me, CancellationToken cancellationToken)
    {
        var question = await _questions.GetAsync(questionId, cancellationToken) ?? throw new NotFoundException(NotFound);
        var product = await _products.GetByIdAsync(question.ProductId, cancellationToken) ?? throw new NotFoundException(NotFound);

        var mayAnswer = product.SellerId is { } seller ? seller == me : IsStaff();
        if (!mayAnswer)
            throw new NotFoundException(NotFound);

        return (question, product);
    }

    private async Task<ProductQuestion> ReloadAsync(Guid id, CancellationToken cancellationToken) =>
        await _questions.GetAsync(id, cancellationToken) ?? throw new NotFoundException(NotFound);

    private bool IsStaff() => _currentUser.IsInRole(StaffRoles.Admin) || _currentUser.IsInRole(StaffRoles.Moderator);

    private Guid Caller() =>
        _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

    /// <summary>The first name from the token; for a token from before it carried one, the email's first letter.</summary>
    private string AskerName()
    {
        if (!string.IsNullOrWhiteSpace(_currentUser.GivenName))
            return _currentUser.GivenName.Trim();
        var email = _currentUser.Email;
        return string.IsNullOrEmpty(email) ? "?" : $"{char.ToUpperInvariant(email[0])}.";
    }
}
