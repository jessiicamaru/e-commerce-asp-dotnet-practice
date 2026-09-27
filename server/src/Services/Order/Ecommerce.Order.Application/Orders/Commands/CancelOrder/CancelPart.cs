using Ecommerce.Contracts.Order;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Application.Vouchers;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Email;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Notifications;
using FluentValidation;
using MassTransit;
using MediatR;

namespace Ecommerce.Order.Application.Orders.Commands.CancelOrder;

/// <summary>
/// A seller cancels their own part of a paid order before it ships (#211, specs/104). The seller is the token's
/// subject - no seller id in the request - and an order with nothing of theirs is the one "Sale not found." (specs/034).
/// </summary>
public record CancelSalePartCommand(Guid OrderId, string Reason) : IRequest<SaleDetailResponse>;

/// <summary>An administrator cancels the shop's own part of an order (specs/104 US3).</summary>
public record CancelShopPartCommand(Guid OrderId, string Reason) : IRequest<OrderDetailResponse>;

public class CancelSalePartCommandValidator : AbstractValidator<CancelSalePartCommand>
{
    public CancelSalePartCommandValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
}

public class CancelShopPartCommandValidator : AbstractValidator<CancelShopPartCommand>
{
    public CancelShopPartCommandValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
}

public class CancelPartHandlers(
    IOrderRepository orders,
    IPublishEndpoint publish,
    IVoucherRepository vouchers,
    ICurrentUser currentUser,
    IAuditTrail audit,
    INotifier notifier,
    IEmailSender email) :
    IRequestHandler<CancelSalePartCommand, SaleDetailResponse>,
    IRequestHandler<CancelShopPartCommand, OrderDetailResponse>
{
    private readonly IOrderRepository _orders = orders;
    private readonly IPublishEndpoint _publish = publish;
    private readonly IVoucherRepository _vouchers = vouchers;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditTrail _audit = audit;
    private readonly INotifier _notifier = notifier;
    private readonly IEmailSender _email = email;

    public async Task<SaleDetailResponse> Handle(CancelSalePartCommand request, CancellationToken cancellationToken)
    {
        var seller = _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        switch (await CancelAsync(request.OrderId, seller, request.Reason, PartCancellation.BySeller, cancellationToken))
        {
            case PartCancelOutcome.Cancelled:
            case PartCancelOutcome.AlreadyCancelled:
                return await _orders.GetSaleAsync(request.OrderId, seller, cancellationToken)
                    ?? throw new NotFoundException(Sales.NotFound);
            // One answer for "no such order", "nothing of yours on it" and "not paid" (specs/034).
            case PartCancelOutcome.NotFound:
            case PartCancelOutcome.NotPaid:
                throw new NotFoundException(Sales.NotFound);
            case PartCancelOutcome.OrderCancelled:
                throw new ConflictException(Sales.Cancelled);
            default:
                throw new ConflictException(PartCancellation.Shipped);
        }
    }

    public async Task<OrderDetailResponse> Handle(CancelShopPartCommand request, CancellationToken cancellationToken)
    {
        var outcome = await CancelAsync(request.OrderId, null, request.Reason, PartCancellation.ByStaff, cancellationToken);
        var order = await _orders.GetByIdAsync(request.OrderId, cancellationToken) ?? throw new NotFoundException(Cancellation.NotFound);

        return outcome switch
        {
            PartCancelOutcome.Cancelled or PartCancelOutcome.AlreadyCancelled => OrderMapping.ToDetail(order),
            PartCancelOutcome.NotFound => throw new ConflictException(PartCancellation.NoShopPart),
            PartCancelOutcome.NotPaid => throw new ConflictException(Cancellation.NotPaid),
            PartCancelOutcome.OrderCancelled => throw new ConflictException("This order was cancelled."),
            _ => throw new ConflictException(PartCancellation.Shipped),
        };
    }

    private async Task<PartCancelOutcome> CancelAsync(Guid orderId, Guid? sellerId, string reason, string by, CancellationToken cancellationToken)
    {
        reason = reason.Trim();
        var at = DateTime.UtcNow;

        return await _orders.TryCancelPartAsync(orderId, sellerId, reason, by, at, async (part, ct) =>
        {
            await _audit.RecordAsync(AuditCategory.Order, "PartCancelled", "Order", orderId.ToString(),
                $"{(part.SellerId is null ? "The shop's" : "A seller's")} part cancelled by the {by.ToLowerInvariant()}: {reason}",
                new { Cancelled = false }, new { Cancelled = true, Reason = reason, part.Refund, part.Last, part.PartId },
                cancellationToken: ct);

            if (part.Last)
            {
                // Nothing left to ship: the order is cancelled whole (US2), down specs/039's path - Inventory returns what
                // is left, Payment refunds what is left, every voucher comes back, and the buyer is told as for any
                // cancellation, with the amount less what earlier parts refunded.
                await _publish.Publish(new OrderCancelledEvent(orderId, at, by), ct);
                await _vouchers.ReleaseForOrderAsync(orderId, at, ct);
                await _audit.RecordAsync(AuditCategory.Order, "OrderCancelled", "Order", orderId.ToString(),
                    $"Order cancelled by the {by.ToLowerInvariant()} - its last part", new { Status = "Paid" },
                    new { Status = "Cancelled", CancelledBy = by }, cancellationToken: ct);
                await OrderNotices.WithFactsAsync(_orders, orderId, f => OrderNotices.CancelledAsync(_notifier, _email, f, by, ct), ct);
                return;
            }

            await _publish.Publish(new OrderPartCancelledEvent(
                orderId, part.PartId, part.VariantIds, part.Refund, part.Currency, at, by), ct);
            // That seller's own voucher bought nothing now (research D5); the platform's stays with the order.
            if (part.SellerId is { } seller)
                await _vouchers.ReleaseForSellerAsync(orderId, seller, at, ct);

            await OrderNotices.WithFactsAsync(_orders, orderId, async f =>
            {
                var shop = await _orders.GetSellerNameAsync(orderId, part.SellerId, ct);
                await OrderNotices.PartCancelledAsync(_notifier, f.BuyerId, orderId, shop, reason, ct);
            }, ct);
        }, cancellationToken);
    }
}
