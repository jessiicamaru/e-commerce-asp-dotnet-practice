using Ecommerce.Contracts.Order;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Vouchers;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Email;
using Ecommerce.Shared.Notifications;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Order.Application.Orders.Commands.CancelOrder;

/// <summary>
/// The customer cancels their own paid order, while every parcel is still waiting (specs/039). No user
/// id here - the owner is the token's subject, and someone else's order is "not found" (Constitution IV).
/// </summary>
public record CancelMyOrderCommand(Guid OrderId) : IRequest<OrderDetailResponse>;

/// <summary>Staff cancel any paid order until its first parcel has shipped (specs/039).</summary>
public record CancelOrderCommand(Guid OrderId) : IRequest<OrderDetailResponse>;

public class CancelMyOrderCommandHandler(
    IOrderRepository orders,
    IPublishEndpoint publish,
    ICurrentUser currentUser,
    IAuditTrail audit,
    INotifier notifier,
    ILogger<CancelMyOrderCommandHandler> logger,
    IVoucherRepository vouchers,
    IEmailSender email)
    : IRequestHandler<CancelMyOrderCommand, OrderDetailResponse>
{
    private readonly IVoucherRepository _vouchers = vouchers;
    private readonly IOrderRepository _orders = orders;
    private readonly IPublishEndpoint _publish = publish;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditTrail _audit = audit;
    private readonly INotifier _notifier = notifier;
    private readonly IEmailSender _email = email;
    private readonly ILogger<CancelMyOrderCommandHandler> _logger = logger;

    public async Task<OrderDetailResponse> Handle(CancelMyOrderCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        await CancelStep.RunAsync(_orders, _publish, _audit, _notifier, _email, _vouchers, _logger, request.OrderId, userId, Cancellation.ByCustomer, cancellationToken);

        var order = await _orders.GetByIdForUserAsync(request.OrderId, userId, cancellationToken)
            ?? throw new NotFoundException(Cancellation.NotFound);
        return OrderMapping.ToDetail(order);
    }
}

public class CancelOrderCommandHandler(
    IOrderRepository orders,
    IPublishEndpoint publish,
    IAuditTrail audit,
    INotifier notifier,
    ILogger<CancelOrderCommandHandler> logger,
    IVoucherRepository vouchers,
    IEmailSender email)
    : IRequestHandler<CancelOrderCommand, OrderDetailResponse>
{
    private readonly IVoucherRepository _vouchers = vouchers;
    private readonly IOrderRepository _orders = orders;
    private readonly IPublishEndpoint _publish = publish;
    private readonly IAuditTrail _audit = audit;
    private readonly INotifier _notifier = notifier;
    private readonly IEmailSender _email = email;
    private readonly ILogger<CancelOrderCommandHandler> _logger = logger;

    public async Task<OrderDetailResponse> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        await CancelStep.RunAsync(_orders, _publish, _audit, _notifier, _email, _vouchers, _logger, request.OrderId, null, Cancellation.ByStaff, cancellationToken);

        var order = await _orders.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException(Cancellation.NotFound);
        return OrderMapping.ToDetail(order);
    }
}

internal static class CancelStep
{
    public static async Task RunAsync(
        IOrderRepository orders,
        IPublishEndpoint publish,
        IAuditTrail audit,
        INotifier notifier,
        IEmailSender email,
        IVoucherRepository vouchers,
        ILogger logger,
        Guid orderId,
        Guid? ownerId,
        string by,
        CancellationToken cancellationToken)
    {
        var at = DateTime.UtcNow;

        // The event is staged INSIDE the repository's transaction, after the guarded UPDATE and before the
        // save - so the row and the outbox message commit together or not at all (Principle III), and a
        // refused or repeated cancellation publishes nothing.
        var outcome = await orders.TryCancelAsync(
            orderId,
            ownerId,
            allowWhilePreparing: by == Cancellation.ByStaff,
            by,
            at,
            async ct =>
            {
                await publish.Publish(new OrderCancelledEvent(orderId, at, by), ct);
                // A cancelled order gives its voucher uses back (specs/069) - in this transaction, so once.
                await vouchers.ReleaseForOrderAsync(orderId, at, ct);
                await audit.RecordAsync(
                    AuditCategory.Order, "OrderCancelled", "Order", orderId.ToString(),
                    $"Order cancelled by the {by.ToLowerInvariant()}",
                    new { Status = "Paid" }, new { Status = "Cancelled", CancelledBy = by },
                    cancellationToken: ct);
                await OrderNotices.WithFactsAsync(orders, orderId, facts => OrderNotices.CancelledAsync(notifier, email, facts, by, ct), ct);
            },
            cancellationToken);

        switch (outcome)
        {
            case CancelOutcome.Cancelled:
                logger.LogInformation("Order {OrderId} cancelled by {CancelledBy}", orderId, by);
                return;
            case CancelOutcome.AlreadyCancelled:
                return;
            case CancelOutcome.NotFound:
                throw new NotFoundException(Cancellation.NotFound);
            case CancelOutcome.NotPaid:
                throw new ConflictException(Cancellation.NotPaid);
            case CancelOutcome.BeingPrepared:
                throw new ConflictException(Cancellation.BeingPrepared);
            default:
                throw new ConflictException(Cancellation.Shipped);
        }
    }
}
