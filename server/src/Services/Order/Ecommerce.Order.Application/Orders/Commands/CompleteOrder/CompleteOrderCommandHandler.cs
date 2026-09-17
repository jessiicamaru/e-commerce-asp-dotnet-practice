using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Order.Application.Orders.Commands.CompleteOrder;

public class CompleteOrderCommandHandler(
    IOrderRepository orderRepository,
    ILogger<CompleteOrderCommandHandler> logger
) : IRequestHandler<CompleteOrderCommand, bool>
{
    private readonly IOrderRepository _orderRepository = orderRepository;
    private readonly ILogger<CompleteOrderCommandHandler> _logger = logger;

    public async Task<bool> Handle(CompleteOrderCommand request, CancellationToken cancellationToken)
    {
        var rowsAffected = await _orderRepository.TrySettleAsync(
            request.OrderId,
            OrderStatus.Completed,
            failureReason: null,
            settledAt: request.CompletedAt,
            cancellationToken);

        if (rowsAffected > 0)
        {
            _logger.LogInformation(
                "Order {OrderId} settled as Completed at {CompletedAt:o}.",
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
