using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Notifications;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Order.Application.Orders.Commands.FailOrder;

public class FailOrderCommandHandler(
    IOrderRepository orderRepository,
    INotifier notifier,
    IAuditTrail audit,
    ILogger<FailOrderCommandHandler> logger
) : IRequestHandler<FailOrderCommand, bool>
{
    private readonly INotifier _notifier = notifier;
    private readonly IAuditTrail _audit = audit;

    /// <summary>
    /// The width of <c>orders.FailureReason</c>. The reason on the announcement is unbounded, and a
    /// settlement that fails because the explanation was long is worse than a settlement with a
    /// clipped explanation.
    /// </summary>
    private const int MaxReasonLength = 512;

    private readonly IOrderRepository _orderRepository = orderRepository;
    private readonly ILogger<FailOrderCommandHandler> _logger = logger;

    public async Task<bool> Handle(FailOrderCommand request, CancellationToken cancellationToken)
    {
        var reason = Truncate(request.Reason);

        var rowsAffected = await _orderRepository.TrySettleAsync(
            request.OrderId,
            OrderStatus.Failed,
            failureReason: reason,
            settledAt: request.FailedAt,
            cancellationToken,
            ct => OrderNotices.WithFactsAsync(_orderRepository, request.OrderId, async facts =>
            {
                await OrderNotices.FailedAsync(_notifier, facts, ct);
                await _audit.RecordAsync(
                    AuditCategory.Order, "OrderFailed", "Order", request.OrderId.ToString(),
                    $"Order failed: {reason}", new { Status = "Submitted" }, new { Status = "Failed", FailureReason = reason },
                    cancellationToken: ct);
            }, ct));

        if (rowsAffected > 0)
        {
            _logger.LogInformation(
                "Order {OrderId} settled as Failed at {FailedAt:o}: {Reason}",
                request.OrderId,
                request.FailedAt,
                reason);

            return true;
        }

        var exists = await _orderRepository.ExistsAsync(request.OrderId, cancellationToken);

        if (exists)
        {
            // Includes the case that matters most: a failure notice arriving for an order already
            // recorded as Completed. The guard refuses it, so a settled order is never rewritten by
            // a contradicting message, whichever order they happen to arrive in.
            _logger.LogInformation(
                "Order {OrderId} was already settled; failure notice ignored (reason would have been "
                + "{Reason}).",
                request.OrderId,
                reason);
        }
        else
        {
            _logger.LogWarning(
                "Failure notice for order {OrderId}, which this service does not hold. Discarded "
                + "rather than retried.",
                request.OrderId);
        }

        return false;
    }

    private static string Truncate(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            // The column is nullable, but a failed order with no explanation is the thing this
            // feature exists to stop. Say that the sender sent nothing rather than leaving it null,
            // which would read as "nobody has looked at this yet".
            return "No reason was supplied by the failing step.";
        }

        return reason.Length <= MaxReasonLength ? reason : reason[..MaxReasonLength];
    }
}
