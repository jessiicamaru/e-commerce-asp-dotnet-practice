using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Email;
using Ecommerce.Shared.Notifications;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Order.Application.Orders.Commands.CompleteOrder;

public class CompleteOrderCommandHandler(
    IOrderRepository orderRepository,
    INotifier notifier,
    IEmailSender email,
    IAuditTrail audit,
    ILogger<CompleteOrderCommandHandler> logger
) : IRequestHandler<CompleteOrderCommand, bool>
{
    private readonly IOrderRepository _orderRepository = orderRepository;
    private readonly INotifier _notifier = notifier;
    private readonly IEmailSender _email = email;
    private readonly IAuditTrail _audit = audit;
    private readonly ILogger<CompleteOrderCommandHandler> _logger = logger;

    public async Task<bool> Handle(CompleteOrderCommand request, CancellationToken cancellationToken)
    {
        var rowsAffected = await _orderRepository.TrySettleAsync(
            request.OrderId,
            // Paid, not Completed (feature 011). The saga's "completed" means checkout completed; for
            // the order that is the moment it is paid, and fulfilment starts from here.
            OrderStatus.Paid,
            failureReason: null,
            settledAt: request.CompletedAt,
            cancellationToken,
            // Paid: the buyer is told, each seller learns of a new sale, and the log records it (specs/041, 042).
            ct => OrderNotices.WithFactsAsync(_orderRepository, request.OrderId, async facts =>
            {
                await OrderNotices.PaidAsync(_notifier, _email, facts, ct);
                await _audit.RecordAsync(
                    AuditCategory.Order, "OrderPaid", "Order", request.OrderId.ToString(),
                    $"Order paid: {facts.Total} {facts.Currency}",
                    new { Status = "Submitted" }, new { Status = "Paid" }, cancellationToken: ct);
            }, ct));

        if (rowsAffected > 0)
        {
            _logger.LogInformation(
                "Order {OrderId} settled as Paid at {CompletedAt:o}.",
                request.OrderId,
                request.CompletedAt);

            return true;
        }

        // Zero rows has two causes that look identical from here and mean different things to
        // whoever is reading the log at 2am. One extra read, only on this path, tells them apart.
        var exists = await _orderRepository.ExistsAsync(request.OrderId, cancellationToken);

        if (exists)
        {
            _logger.LogInformation(
                "Order {OrderId} was already settled; completion notice ignored. This is ordinary — "
                + "the broker redelivers.",
                request.OrderId);
        }
        else
        {
            _logger.LogWarning(
                "Completion notice for order {OrderId}, which this service does not hold. Discarded "
                + "rather than retried: the order will not appear later, and redelivering forever "
                + "would fault the endpoint.",
                request.OrderId);
        }

        return false;
    }
}
