using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Notifications;
using FluentValidation;
using MediatR;

namespace Ecommerce.Order.Application.Orders.Commands.CorrectTracking;

/// <summary>What became of a request to correct a shipped part's tracking reference (specs/105).</summary>
public enum TrackingCorrectionOutcome
{
    Corrected,
    /// <summary>It already reads that - a repeat, changing and telling nothing.</summary>
    Unchanged,
    NotFound,
    NotShipped,
    Delivered,
    PartCancelled,
    OrderCancelled
}

/// <summary>A seller corrects the tracking reference of THEIR shipped part (#212, specs/105).</summary>
public record CorrectSaleTrackingCommand(Guid OrderId, string TrackingReference) : IRequest<SaleDetailResponse>;

/// <summary>An administrator corrects the shop's own part's (specs/105).</summary>
public record CorrectShopTrackingCommand(Guid OrderId, string TrackingReference) : IRequest<OrderDetailResponse>;

public class CorrectSaleTrackingCommandValidator : AbstractValidator<CorrectSaleTrackingCommand>
{
    public CorrectSaleTrackingCommandValidator() => RuleFor(x => x.TrackingReference).NotEmpty().MaximumLength(100)
        .WithMessage("A tracking reference of at most 100 characters is required.");
}

public class CorrectShopTrackingCommandValidator : AbstractValidator<CorrectShopTrackingCommand>
{
    public CorrectShopTrackingCommandValidator() => RuleFor(x => x.TrackingReference).NotEmpty().MaximumLength(100)
        .WithMessage("A tracking reference of at most 100 characters is required.");
}

public class CorrectTrackingHandlers(IOrderRepository orders, ICurrentUser currentUser, IAuditTrail audit, INotifier notifier) :
    IRequestHandler<CorrectSaleTrackingCommand, SaleDetailResponse>,
    IRequestHandler<CorrectShopTrackingCommand, OrderDetailResponse>
{
    private readonly IOrderRepository _orders = orders;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditTrail _audit = audit;
    private readonly INotifier _notifier = notifier;

    public async Task<SaleDetailResponse> Handle(CorrectSaleTrackingCommand request, CancellationToken cancellationToken)
    {
        var seller = _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        var outcome = await CorrectAsync(request.OrderId, seller, request.TrackingReference, cancellationToken);
        if (outcome == TrackingCorrectionOutcome.NotFound)
            throw new NotFoundException(Sales.NotFound);   // one answer for "not yours" and "no such sale" (specs/034)
        Refuse(outcome);

        return await _orders.GetSaleAsync(request.OrderId, seller, cancellationToken) ?? throw new NotFoundException(Sales.NotFound);
    }

    public async Task<OrderDetailResponse> Handle(CorrectShopTrackingCommand request, CancellationToken cancellationToken)
    {
        var outcome = await CorrectAsync(request.OrderId, null, request.TrackingReference, cancellationToken);
        var order = await _orders.GetByIdAsync(request.OrderId, cancellationToken) ?? throw new NotFoundException(Cancellation.NotFound);
        if (outcome == TrackingCorrectionOutcome.NotFound)
            throw new ConflictException("This order has no part the shop ships; each seller corrects their own.");
        Refuse(outcome);

        return OrderMapping.ToDetail(order);
    }

    private static void Refuse(TrackingCorrectionOutcome outcome)
    {
        switch (outcome)
        {
            case TrackingCorrectionOutcome.NotShipped:
                throw new ConflictException("This part has not been shipped; there is no tracking reference to correct.");
            case TrackingCorrectionOutcome.Delivered:
                throw new ConflictException("This part has been delivered; there is nothing left to track.");
            case TrackingCorrectionOutcome.PartCancelled:
                throw new ConflictException(PartCancellation.Cancelled);
            case TrackingCorrectionOutcome.OrderCancelled:
                throw new ConflictException("This order was cancelled.");
        }
    }

    private Task<TrackingCorrectionOutcome> CorrectAsync(Guid orderId, Guid? sellerId, string reference, CancellationToken cancellationToken)
    {
        reference = reference.Trim();

        return _orders.TryCorrectTrackingAsync(orderId, sellerId, reference, DateTime.UtcNow, async (old, ct) =>
        {
            // Every correction is on the record, old and new (research D2) - the trail is what deters hiding a parcel.
            await _audit.RecordAsync(AuditCategory.Order, "TrackingCorrected", "Order", orderId.ToString(),
                $"{(sellerId is null ? "The shop's" : "A seller's")} tracking reference corrected: {old} -> {reference}",
                new { Seller = sellerId, TrackingReference = old }, new { Seller = sellerId, TrackingReference = reference },
                cancellationToken: ct);

            // And the buyer is told, every time - so they follow the right parcel, and see if it keeps changing.
            await OrderNotices.WithFactsAsync(_orders, orderId, async f =>
            {
                var shop = await _orders.GetSellerNameAsync(orderId, sellerId, ct);
                await OrderNotices.TrackingCorrectedAsync(_notifier, f.BuyerId, orderId, shop, reference, ct);
            }, ct);
        }, cancellationToken);
    }
}
