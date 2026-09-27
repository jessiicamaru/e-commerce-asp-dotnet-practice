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

namespace Ecommerce.Catalog.Application.Reports;

/// <summary>A shopper reports a review, a question or a product (#199, specs/101).</summary>
public record ReportContentCommand(string TargetType, Guid TargetId, string Reason, string? Details) : IRequest<ReportResponse>;

public record ReportResponse(Guid Id, string TargetType, Guid TargetId, string Reason, DateTime CreatedAt);

/// <summary>The moderators' queue: open reports, one row per thing, most reported first.</summary>
public record GetReportQueueQuery(int PageNumber = 1, int PageSize = 12) : IRequest<PaginatedList<ReportedItemResponse>>;

/// <param name="Excerpt">What was reported, as it reads now: the review or question text, or the product's name.</param>
public record ReportedItemResponse(
    string TargetType,
    Guid TargetId,
    Guid ProductId,
    string ProductName,
    string? Excerpt,
    int ReportCount,
    Dictionary<string, int> Reasons,
    List<string> Details,
    DateTime FirstReportedAt,
    DateTime LastReportedAt);

/// <summary>Staff looked and are leaving it as it is: every open report of the thing closes, and each reporter is told.</summary>
public record DismissReportsCommand(string TargetType, Guid TargetId) : IRequest;

public class ReportContentCommandValidator : AbstractValidator<ReportContentCommand>
{
    public ReportContentCommandValidator()
    {
        RuleFor(x => x.TargetType).Must(t => Enum.TryParse<ReportTarget>(t, false, out _))
            .WithMessage("TargetType must be one of: " + string.Join(", ", Enum.GetNames<ReportTarget>()) + ".");
        RuleFor(x => x.Reason).Must(r => Enum.TryParse<ReportReason>(r, false, out _))
            .WithMessage("Reason must be one of: " + string.Join(", ", Enum.GetNames<ReportReason>()) + ".");
        RuleFor(x => x.Details).MaximumLength(500);
    }
}

public class DismissReportsCommandValidator : AbstractValidator<DismissReportsCommand>
{
    public DismissReportsCommandValidator()
    {
        RuleFor(x => x.TargetType).Must(t => Enum.TryParse<ReportTarget>(t, false, out _))
            .WithMessage("TargetType must be one of: " + string.Join(", ", Enum.GetNames<ReportTarget>()) + ".");
    }
}

public class GetReportQueueQueryValidator : AbstractValidator<GetReportQueueQuery>
{
    public GetReportQueueQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

/// <summary>
/// Closing a thing's reports when staff act on it (specs/101). Called from inside the stage callback of the hide or
/// take-down that decided it, so the reports close in that transaction - whichever page the moderator used.
/// </summary>
public static class ContentReports
{
    public static async Task CloseAsync(
        IContentReportRepository reports,
        INotifier notifier,
        ReportTarget targetType,
        Guid targetId,
        Guid productId,
        string productName,
        ReportStatus outcome,
        Guid by,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var reporters = await reports.CloseAsync(targetType, targetId, outcome, by, now, cancellationToken);

        // Told how it ended, never by whom (the issue): the notice carries the product, not the moderator.
        var kind = outcome == ReportStatus.Actioned ? NotificationKind.ReportActioned : NotificationKind.ReportDismissed;
        foreach (var reporter in reporters)
        {
            await notifier.NotifyAsync(reporter, kind, new Dictionary<string, string> { ["product"] = productName },
                $"/products/{productId}", cancellationToken);
        }
    }
}

public class ReportHandlers(
    IContentReportRepository reports,
    IReviewRepository reviews,
    IProductQuestionRepository questions,
    IProductRepository products,
    ICurrentUser currentUser,
    IAuditTrail audit,
    INotifier notifier) :
    IRequestHandler<ReportContentCommand, ReportResponse>,
    IRequestHandler<GetReportQueueQuery, PaginatedList<ReportedItemResponse>>,
    IRequestHandler<DismissReportsCommand>
{
    private readonly IContentReportRepository _reports = reports;
    private readonly IReviewRepository _reviews = reviews;
    private readonly IProductQuestionRepository _questions = questions;
    private readonly IProductRepository _products = products;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditTrail _audit = audit;
    private readonly INotifier _notifier = notifier;

    public async Task<ReportResponse> Handle(ReportContentCommand request, CancellationToken cancellationToken)
    {
        var me = Caller();
        var target = Enum.Parse<ReportTarget>(request.TargetType);
        var (productId, authorId) = await WhatIsReportedAsync(target, request.TargetId, cancellationToken);

        // Your own words are yours to edit or delete, not to report.
        if (authorId == me)
            throw new ConflictException($"You cannot report your own {target.ToString().ToLowerInvariant()}.");

        var report = new ContentReport
        {
            TargetType = target,
            TargetId = request.TargetId,
            ProductId = productId,
            ReporterId = me,
            Reason = Enum.Parse<ReportReason>(request.Reason),
            Details = string.IsNullOrWhiteSpace(request.Details) ? null : request.Details.Trim(),
            Status = ReportStatus.Open,
            CreatedAt = DateTime.UtcNow,
        };
        if (!await _reports.TryAddAsync(report, cancellationToken))
            throw new ConflictException("You have already reported this; staff will look at it.");

        return new ReportResponse(report.Id, target.ToString(), report.TargetId, report.Reason.ToString(), report.CreatedAt);
    }

    public async Task<PaginatedList<ReportedItemResponse>> Handle(GetReportQueueQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _reports.GetQueueAsync(request.PageNumber, request.PageSize, cancellationToken);

        var list = new List<ReportedItemResponse>(items.Count);
        foreach (var i in items)
        {
            var excerpt = i.TargetType switch
            {
                ReportTarget.Review => (await _reviews.GetAsync(i.TargetId, cancellationToken))?.Body,
                ReportTarget.Question => (await _questions.GetAsync(i.TargetId, cancellationToken))?.Body,
                _ => i.ProductName,
            };
            list.Add(new ReportedItemResponse(i.TargetType.ToString(), i.TargetId, i.ProductId, i.ProductName, excerpt,
                i.ReportCount, i.Reasons, i.Details, i.FirstReportedAt, i.LastReportedAt));
        }

        return new PaginatedList<ReportedItemResponse>(list, total, request.PageNumber, request.PageSize);
    }

    public async Task Handle(DismissReportsCommand request, CancellationToken cancellationToken)
    {
        var by = Caller();
        var target = Enum.Parse<ReportTarget>(request.TargetType);
        var now = DateTime.UtcNow;

        // Staff may dismiss reports of something that has since left the shelf, so this reads without the shopper's rules.
        var productId = target switch
        {
            ReportTarget.Review => (await _reviews.GetAsync(request.TargetId, cancellationToken))?.ProductId,
            ReportTarget.Question => (await _questions.GetAsync(request.TargetId, cancellationToken))?.ProductId,
            _ => request.TargetId,
        };
        var product = productId is { } id ? await _products.GetByIdAsync(id, cancellationToken) : null;

        var closed = await _reports.InTransactionAsync(async ct =>
        {
            var reporters = await _reports.CloseAsync(target, request.TargetId, ReportStatus.Dismissed, by, now, ct);
            if (reporters.Count == 0) return 0;

            await _audit.RecordAsync(AuditCategory.Moderation, "ReportsDismissed", target.ToString(), request.TargetId.ToString(),
                $"{reporters.Count} report(s) of a {target.ToString().ToLowerInvariant()} dismissed",
                new { Open = reporters.Count }, new { Open = 0, Outcome = "Dismissed" }, cancellationToken: ct);
            foreach (var reporter in reporters)
            {
                await _notifier.NotifyAsync(reporter, NotificationKind.ReportDismissed,
                    new Dictionary<string, string> { ["product"] = product?.Name ?? "" },
                    product is null ? null : $"/products/{product.Id}", ct);
            }

            return reporters.Count;
        }, cancellationToken);

        if (closed == 0)
            throw new ConflictException("Nothing reported about this is waiting.");
    }

    /// <summary>
    /// The product a report hangs on and whose words it is. Only what a shopper can see can be reported: anything
    /// hidden or off the shelf is the same 404 the public pages give.
    /// </summary>
    private async Task<(Guid ProductId, Guid? AuthorId)> WhatIsReportedAsync(ReportTarget target, Guid id, CancellationToken ct)
    {
        switch (target)
        {
            case ReportTarget.Review:
            {
                var review = await _reviews.GetAsync(id, ct);
                if (review is null || review.HiddenAt is not null || !await OnShelfAsync(review.ProductId, ct))
                    throw new NotFoundException("Review not found.");
                return (review.ProductId, review.CustomerId);
            }
            case ReportTarget.Question:
            {
                var question = await _questions.GetAsync(id, ct);
                if (question is null || question.HiddenAt is not null || !await OnShelfAsync(question.ProductId, ct))
                    throw new NotFoundException("Question not found.");
                return (question.ProductId, question.AskerId);
            }
            default:
            {
                var product = await _products.GetByIdAsync(id, ct);
                if (product is null || !product.OnShelf)
                    throw new NotFoundException($"Product with ID '{id}' was not found.");
                return (product.Id, product.SellerId);
            }
        }
    }

    private async Task<bool> OnShelfAsync(Guid productId, CancellationToken ct) =>
        (await _products.GetByIdAsync(productId, ct))?.OnShelf == true;

    private Guid Caller() =>
        _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");
}
