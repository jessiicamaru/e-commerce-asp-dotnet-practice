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

namespace Ecommerce.Catalog.Application.Reviews;

/// <summary>A review as a shopper reads it (specs/046). Hidden ones carry why, for staff.</summary>
public record ReviewResponse(
    Guid Id,
    Guid ProductId,
    string AuthorName,
    int Rating,
    string? Body,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool Edited,
    string? ProductName = null,
    DateTime? HiddenAt = null,
    string? HiddenReason = null)
{
    public static ReviewResponse From(Review r, string? productName = null) => new(
        r.Id, r.ProductId, r.AuthorName, r.Rating, r.Body, r.CreatedAt, r.UpdatedAt,
        r.UpdatedAt > r.CreatedAt.AddSeconds(1), productName, r.HiddenAt, r.HiddenReason);
}

/// <summary>Whether the caller may review this product, and their review if they wrote one.</summary>
public record MyReviewResponse(bool Eligible, ReviewResponse? Review);

/// <summary>Records who received which products - from Order's delivery event (specs/046).</summary>
public record RecordReviewEligibilityCommand(Guid CustomerId, List<Guid> ProductIds, DateTime DeliveredAt) : IRequest;

public record GetProductReviewsQuery(Guid ProductId, int PageNumber = 1, int PageSize = 12) : IRequest<PaginatedList<ReviewResponse>>;

public record GetMyReviewQuery(Guid ProductId) : IRequest<MyReviewResponse>;

/// <summary>Writes the caller's review of a product, or rewrites it: one per customer per product.</summary>
public record WriteReviewCommand(Guid ProductId, int Rating, string? Body) : IRequest<ReviewResponse>;

public record GetReviewsForStaffQuery(bool Hidden = false, int PageNumber = 1, int PageSize = 12) : IRequest<PaginatedList<ReviewResponse>>;

public record HideReviewCommand(Guid ReviewId, string Reason) : IRequest<ReviewResponse>;

public record RestoreReviewCommand(Guid ReviewId) : IRequest<ReviewResponse>;

public class WriteReviewCommandValidator : AbstractValidator<WriteReviewCommand>
{
    public WriteReviewCommandValidator()
    {
        RuleFor(x => x.Rating).InclusiveBetween(1, 5).WithMessage("Rating must be from 1 to 5 stars.");
        RuleFor(x => x.Body).MaximumLength(2000);
    }
}

public class HideReviewCommandValidator : AbstractValidator<HideReviewCommand>
{
    public HideReviewCommandValidator() =>
        RuleFor(x => x.Reason).Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("Reason is required.").MaximumLength(500);
}

public class PagingValidators :
    AbstractValidator<GetProductReviewsQuery>
{
    public PagingValidators()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}

public class ReviewHandlers(
    IReviewRepository reviews,
    IProductRepository products,
    ICurrentUser currentUser,
    IAuditTrail audit,
    INotifier notifier) :
    IRequestHandler<RecordReviewEligibilityCommand>,
    IRequestHandler<GetProductReviewsQuery, PaginatedList<ReviewResponse>>,
    IRequestHandler<GetMyReviewQuery, MyReviewResponse>,
    IRequestHandler<WriteReviewCommand, ReviewResponse>,
    IRequestHandler<GetReviewsForStaffQuery, PaginatedList<ReviewResponse>>,
    IRequestHandler<HideReviewCommand, ReviewResponse>,
    IRequestHandler<RestoreReviewCommand, ReviewResponse>
{
    public const string NotEligible = "Only a customer who has received this product can review it.";
    public const string OwnProduct = "You cannot review your own product.";

    private readonly IReviewRepository _reviews = reviews;
    private readonly IProductRepository _products = products;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditTrail _audit = audit;
    private readonly INotifier _notifier = notifier;

    public Task Handle(RecordReviewEligibilityCommand request, CancellationToken cancellationToken) =>
        _reviews.RecordEligibilityAsync(request.CustomerId, request.ProductIds, request.DeliveredAt, cancellationToken);

    public async Task<PaginatedList<ReviewResponse>> Handle(GetProductReviewsQuery request, CancellationToken cancellationToken)
    {
        // The public lookup's rule (specs/045, #166): off the shelf, only its seller and staff read what is said about it.
        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null || !ProductReview.MaySee(product, _currentUser))
            throw new NotFoundException("Product not found.");

        var (items, total) = await _reviews.GetVisibleAsync(request.ProductId, request.PageNumber, request.PageSize, cancellationToken);
        // What a shopper reads: no reason, no hider - those are for staff.
        return new PaginatedList<ReviewResponse>(items.Select(r => ReviewResponse.From(r)).ToList(), total, request.PageNumber, request.PageSize);
    }

    public async Task<MyReviewResponse> Handle(GetMyReviewQuery request, CancellationToken cancellationToken)
    {
        var me = Caller();
        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken);
        // The page says what the command would: a seller is never eligible for their own product (#127).
        var eligible = product?.SellerId != me && await _reviews.IsEligibleAsync(request.ProductId, me, cancellationToken);
        var mine = await _reviews.GetMineAsync(request.ProductId, me, cancellationToken);
        return new MyReviewResponse(eligible, mine is null ? null : ReviewResponse.From(mine));
    }

    public async Task<ReviewResponse> Handle(WriteReviewCommand request, CancellationToken cancellationToken)
    {
        var me = Caller();
        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException($"Product with ID '{request.ProductId}' was not found.");

        // A seller who bought their own camera does not rate it: the one signal a shopper reads as
        // independent (#127, specs/057).
        if (product.SellerId == me)
            throw new ForbiddenException(OwnProduct);

        // Received it, or no review - a 403 in words rather than a quiet refusal (the issue's acceptance).
        if (!await _reviews.IsEligibleAsync(product.Id, me, cancellationToken))
            throw new ForbiddenException(NotEligible);

        var now = DateTime.UtcNow;
        var body = string.IsNullOrWhiteSpace(request.Body) ? null : request.Body.Trim();

        if (await _reviews.GetMineAsync(product.Id, me, cancellationToken) is null)
        {
            var first = new Review
            {
                ProductId = product.Id,
                CustomerId = me,
                AuthorName = AuthorName(),
                Rating = request.Rating,
                Body = body,
                CreatedAt = now,
                UpdatedAt = now,
            };

            // Guarded (#127): two first reviews at once (a double-click, two tabs) both got here. One inserts
            // and is recorded and announced; the other inserts nothing and edits it below - where before the
            // unique index refused it with a 500. Nothing is staged for an insert that did not happen.
            var inserted = await _reviews.TryAddFirstAsync(first, async ct =>
            {
                await _audit.RecordAsync(AuditCategory.Catalog, "ReviewPosted", "Review", first.Id.ToString(),
                    $"{request.Rating}-star review of \"{product.Name}\"", after: new { first.Rating, first.Body },
                    cancellationToken: ct);

                // The seller hears about a new review, not about every edit of it.
                if (product.SellerId is { } seller)
                {
                    await _notifier.NotifyAsync(seller, NotificationKind.NewReview,
                        new Dictionary<string, string> { ["product"] = product.Name, ["rating"] = request.Rating.ToString() },
                        $"/products/{product.Id}", ct);
                }
            }, cancellationToken);

            if (inserted == 1)
                return ReviewResponse.From(first);
        }

        var review = await _reviews.GetMineAsync(product.Id, me, cancellationToken)
            ?? throw new InvalidOperationException("A review that refused a second insert must exist.");
        var before = new { review.Rating, review.Body };
        review.Rating = request.Rating;
        review.Body = body;
        review.UpdatedAt = now;
        await _audit.RecordAsync(AuditCategory.Catalog, "ReviewEdited", "Review", review.Id.ToString(),
            $"Review of \"{product.Name}\" edited", before, new { review.Rating, review.Body },
            cancellationToken: cancellationToken);

        await _reviews.SaveAndRecomputeAsync(product.Id, cancellationToken);
        return ReviewResponse.From(review);
    }

    public async Task<PaginatedList<ReviewResponse>> Handle(GetReviewsForStaffQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _reviews.GetForStaffAsync(request.Hidden, request.PageNumber, request.PageSize, cancellationToken);
        return new PaginatedList<ReviewResponse>(
            items.Select(r => ReviewResponse.From(r.Review, r.ProductName)).ToList(), total, request.PageNumber, request.PageSize);
    }

    /// <summary>Hidden, not deleted: it leaves the page and the average, and a moderator can put it back.</summary>
    public async Task<ReviewResponse> Handle(HideReviewCommand request, CancellationToken cancellationToken)
    {
        var review = await _reviews.GetAsync(request.ReviewId, cancellationToken) ?? throw new NotFoundException("Review not found.");
        var reason = request.Reason.Trim();
        var by = Caller();
        var now = DateTime.UtcNow;

        var product = await _products.GetByIdAsync(review.ProductId, cancellationToken);

        // One guarded statement decides (#127): two moderators at once, one hides and the other is told.
        var hidden = await _reviews.TryHideAsync(review.Id, review.ProductId, reason, by, now, async ct =>
        {
            await _audit.RecordAsync(AuditCategory.Moderation, "ReviewHidden", "Review", review.Id.ToString(),
                $"A {review.Rating}-star review hidden: {reason}",
                new { Hidden = false }, new { Hidden = true, Reason = reason }, cancellationToken: ct);

            // Its author learns why it disappeared (#128, specs/059).
            await _notifier.NotifyAsync(review.CustomerId, NotificationKind.ReviewHidden,
                new Dictionary<string, string> { ["product"] = product?.Name ?? "", ["reason"] = reason },
                $"/products/{review.ProductId}", ct);
        }, cancellationToken);
        if (hidden == 0)
            throw new ConflictException("This review is already hidden.");

        (review.HiddenAt, review.HiddenReason, review.HiddenBy) = (now, reason, by);
        return ReviewResponse.From(review);
    }

    public async Task<ReviewResponse> Handle(RestoreReviewCommand request, CancellationToken cancellationToken)
    {
        var review = await _reviews.GetAsync(request.ReviewId, cancellationToken) ?? throw new NotFoundException("Review not found.");
        var reason = review.HiddenReason;

        var restored = await _reviews.TryRestoreAsync(review.Id, review.ProductId, ct =>
            _audit.RecordAsync(AuditCategory.Moderation, "ReviewRestored", "Review", review.Id.ToString(),
                $"A {review.Rating}-star review shown again",
                new { Hidden = true, Reason = reason }, new { Hidden = false }, cancellationToken: ct), cancellationToken);
        if (restored == 0)
            throw new ConflictException("This review is not hidden.");

        (review.HiddenAt, review.HiddenReason, review.HiddenBy) = (null, null, null);
        return ReviewResponse.From(review);
    }

    private Guid Caller() =>
        _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

    /// <summary>The first name from the token; for a token from before it carried one, the email's first letter.</summary>
    private string AuthorName()
    {
        if (!string.IsNullOrWhiteSpace(_currentUser.GivenName))
            return _currentUser.GivenName.Trim();
        var email = _currentUser.Email;
        return string.IsNullOrEmpty(email) ? "?" : $"{char.ToUpperInvariant(email[0])}.";
    }
}
