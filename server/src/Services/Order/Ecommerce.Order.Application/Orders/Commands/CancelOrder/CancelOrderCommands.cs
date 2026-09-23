using Ecommerce.Contracts.Order;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
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
    ILogger<CancelMyOrderCommandHandler> logger)
    : IRequestHandler<CancelMyOrderCommand, OrderDetailResponse>
{
    private readonly IOrderRepository _orders = orders;
    private readonly IPublishEndpoint _publish = publish;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<CancelMyOrderCommandHandler> _logger = logger;

    public async Task<OrderDetailResponse> Handle(CancelMyOrderCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        await CancelStep.RunAsync(_orders, _publish, _logger, request.OrderId, userId, Cancellation.ByCustomer, cancellationToken);

        var order = await _orders.GetByIdForUserAsync(request.OrderId, userId, cancellationToken)
            ?? throw new NotFoundException(Cancellation.NotFound);
        return OrderMapping.ToDetail(order);
    }
}

public class CancelOrderCommandHandler(
    IOrderRepository orders,
    IPublishEndpoint publish,
    ILogger<CancelOrderCommandHandler> logger)
    : IRequestHandler<CancelOrderCommand, OrderDetailResponse>
{
    private readonly IOrderRepository _orders = orders;
    private readonly IPublishEndpoint _publish = publish;
    private readonly ILogger<CancelOrderCommandHandler> _logger = logger;

    public async Task<OrderDetailResponse> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        await CancelStep.RunAsync(_orders, _publish, _logger, request.OrderId, null, Cancellation.ByStaff, cancellationToken);

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
            ct => publish.Publish(new OrderCancelledEvent(orderId, at, by), ct),
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
