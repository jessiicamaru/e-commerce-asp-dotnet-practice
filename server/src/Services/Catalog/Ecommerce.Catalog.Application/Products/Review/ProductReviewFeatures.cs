using Ecommerce.Catalog.Application.Common;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Common.Models;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Application.Products.Saved;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Email;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Localization;
using Ecommerce.Shared.Money;
using Ecommerce.Shared.Notifications;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace Ecommerce.Catalog.Application.Products.Review;

/// <summary>Staff read the queue (pending, oldest first) or the history of one review status (specs/045).</summary>
public record GetReviewQueueQuery(string Status = "Pending", int PageNumber = 1, int PageSize = 12) : IRequest<PaginatedList<ProductResponse>>;

public class GetReviewQueueQueryValidator : AbstractValidator<GetReviewQueueQuery>
{
    public GetReviewQueueQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.Status)
            .Must(s => Enum.TryParse<ProductReviewStatus>(s, ignoreCase: true, out _))
            .WithMessage("Status must be Pending, Approved or Rejected.");
    }
}

public record ApproveProductCommand(Guid ProductId) : IRequest<ProductResponse>;

/// <summary>Refuses a pending product, with the reason its seller reads.</summary>
public record RejectProductCommand(Guid ProductId, string Reason) : IRequest<ProductResponse>;

/// <summary>Takes an approved product off the shelf, with the reason its seller reads.</summary>
public record TakeDownProductCommand(Guid ProductId, string Reason) : IRequest<ProductResponse>;

/// <summary>A seller sends a rejected product back to the queue, having changed what they were told to.</summary>
public record ResubmitProductCommand(Guid ProductId) : IRequest<ProductResponse>;

public class RejectProductCommandValidator : AbstractValidator<RejectProductCommand>
{
    public RejectProductCommandValidator() =>
        RuleFor(x => x.Reason).Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("Reason is required.").MaximumLength(500);
}

public class TakeDownProductCommandValidator : AbstractValidator<TakeDownProductCommand>
{
    public TakeDownProductCommandValidator() =>
        RuleFor(x => x.Reason).Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("Reason is required.").MaximumLength(500);
}

public class ProductReviewHandlers(
    IProductRepository products,
    ISellerRepository sellers,
    ICurrentUser currentUser,
    IAuditTrail audit,
    INotifier notifier,
    IRequestLanguage language,
    IOptions<LanguageOptions> localization,
    IRequestCurrency currency,
    IOptions<CurrencyOptions> money,
    ISavedProductRepository saved,
    IEmailSender email) :
    IRequestHandler<GetReviewQueueQuery, PaginatedList<ProductResponse>>,
    IRequestHandler<ApproveProductCommand, ProductResponse>,
    IRequestHandler<RejectProductCommand, ProductResponse>,
    IRequestHandler<TakeDownProductCommand, ProductResponse>,
    IRequestHandler<ResubmitProductCommand, ProductResponse>
{
    private readonly IProductRepository _products = products;
    private readonly ISellerRepository _sellers = sellers;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditTrail _audit = audit;
    private readonly INotifier _notifier = notifier;
    private readonly ISavedProductRepository _saved = saved;
    private readonly IEmailSender _email = email;

    public async Task<PaginatedList<ProductResponse>> Handle(GetReviewQueueQuery request, CancellationToken cancellationToken)
    {
        var status = Enum.Parse<ProductReviewStatus>(request.Status, ignoreCase: true);
        var (items, total) = await _products.GetForReviewAsync(status, request.PageNumber, request.PageSize, cancellationToken);

        var sellerIds = items.Where(p => p.SellerId is not null).Select(p => p.SellerId!.Value).Distinct().ToList();
        var names = sellerIds.Count == 0 ? [] : await _sellers.GetNamesAsync(sellerIds, cancellationToken);

        var dtos = items.Select(p => Respond(p, p.SellerId is { } id && names.TryGetValue(id, out var n) ? n : null)).ToList();
        return new PaginatedList<ProductResponse>(dtos, total, request.PageNumber, request.PageSize);
    }

    public Task<ProductResponse> Handle(ApproveProductCommand request, CancellationToken cancellationToken) =>
        MoveAsync(request.ProductId, [ProductReviewStatus.Pending], ProductReviewStatus.Approved, null,
            "ProductApproved", NotificationKind.ProductApproved, "approved", cancellationToken);

    public Task<ProductResponse> Handle(RejectProductCommand request, CancellationToken cancellationToken) =>
        MoveAsync(request.ProductId, [ProductReviewStatus.Pending], ProductReviewStatus.Rejected, request.Reason.Trim(),
            "ProductRejected", NotificationKind.ProductRejected, "rejected", cancellationToken);

    public Task<ProductResponse> Handle(TakeDownProductCommand request, CancellationToken cancellationToken) =>
        MoveAsync(request.ProductId, [ProductReviewStatus.Approved], ProductReviewStatus.Rejected, request.Reason.Trim(),
            "ProductTakenDown", NotificationKind.ProductTakenDown, "taken down", cancellationToken);

    public async Task<ProductResponse> Handle(ResubmitProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException($"Product with ID '{request.ProductId}' was not found.");
        SellerOwnership.RequireCanWrite(product, _currentUser);

        var moved = await _products.TryReviewAsync(product.Id, [ProductReviewStatus.Rejected], ProductReviewStatus.Pending,
            null, null, DateTime.UtcNow,
            ct => _audit.RecordAsync(AuditCategory.Moderation, "ProductResubmitted", "Product", product.Id.ToString(),
                $"\"{product.Name}\" sent back for review", new { ReviewStatus = "Rejected" }, new { ReviewStatus = "Pending" },
                cancellationToken: ct),
            cancellationToken);

        return await AfterAsync(product.Id, moved, cancellationToken);
    }

    /// <summary>
    /// One review move: the guarded update, then - only for the call that won it - the audit entry and the
    /// seller's notice, in the same transaction.
    /// </summary>
    private async Task<ProductResponse> MoveAsync(
        Guid productId,
        ProductReviewStatus[] from,
        ProductReviewStatus to,
        string? reason,
        string action,
        string kind,
        string verb,
        CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(productId, cancellationToken)
            ?? throw new NotFoundException($"Product with ID '{productId}' was not found.");
        var reviewer = _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        var moved = await _products.TryReviewAsync(product.Id, from, to, reason, reviewer, DateTime.UtcNow, async ct =>
        {
            await _audit.RecordAsync(AuditCategory.Moderation, action, "Product", product.Id.ToString(),
                $"\"{product.Name}\" {verb}" + (reason is null ? "" : $": {reason}"),
                new { ReviewStatus = product.ReviewStatus.ToString() }, new { ReviewStatus = to.ToString(), Reason = reason },
                cancellationToken: ct);

            // The shop's own products have nobody to tell.
            if (product.SellerId is { } seller)
            {
                var data = new Dictionary<string, string> { ["product"] = product.Name };
                if (reason is not null) data["reason"] = reason;
                await _notifier.NotifyAsync(seller, kind, data, $"/shop/products/{product.Id}", ct);
            }

            // Back on the shelf and in stock: for whoever saved it while it was listed, the product is back (#182,
            // specs/091). Only approval lists a product; the guarded move above makes this call the one that did.
            if (to == ProductReviewStatus.Approved && product.IsActive && product.Availability)
            {
                await SavedProductNotices.BackOnSaleAsync(product, _saved, _notifier, _email, ct);
            }
        }, cancellationToken);

        return await AfterAsync(product.Id, moved, cancellationToken);
    }

    private async Task<ProductResponse> AfterAsync(Guid productId, bool moved, CancellationToken cancellationToken)
    {
        var after = await _products.GetByIdAsync(productId, cancellationToken)
            ?? throw new NotFoundException($"Product with ID '{productId}' was not found.");
        if (!moved)
            throw new ConflictException($"This product is {after.ReviewStatus.ToString().ToLowerInvariant()}; nothing to do.");

        var names = after.SellerId is { } id ? await _sellers.GetNamesAsync([id], cancellationToken) : [];
        return Respond(after, after.SellerId is { } s && names.TryGetValue(s, out var n) ? n : null);
    }

    private ProductResponse Respond(Product product, string? shopName) =>
        ProductResponse.WithVariants(product, language.Current, localization.Value.DefaultLanguage,
            currency.Current.Code, money.Value.DefaultCurrency, shopName);
}
