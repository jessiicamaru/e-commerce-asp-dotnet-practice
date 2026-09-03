using Ecommerce.Contracts.Order;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Domain.Entities;
using Ecommerce.Order.Domain.Enums;
using MassTransit;
using MediatR;

namespace Ecommerce.Order.Application.Orders.Commands.SubmitOrder;

public class SubmitOrderCommandHandler(
    IOrderRepository orderRepository,
    IPublishEndpoint publishEndpoint
) : IRequestHandler<SubmitOrderCommand, OrderResponse>
{
    private readonly IOrderRepository _orderRepository = orderRepository;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;

    public async Task<OrderResponse> Handle(SubmitOrderCommand request, CancellationToken cancellationToken)
    {
        var orderId = Guid.NewGuid();

        var orderItems = request.Items.Select(item => new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProductId = item.ProductId,
            ProductName = item.ProductName,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice
        }).ToList();

        var totalAmount = orderItems.Sum(x => x.TotalPrice);

        var order = new Domain.Entities.Order
        {
            Id = orderId,
            UserId = request.UserId,
            TotalAmount = totalAmount,
            Status = OrderStatus.Submitted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Items = orderItems
        };

        // 1. Stage Order Entity in DbContext
        await _orderRepository.AddAsync(order, cancellationToken);

        // 2. Publish Domain Event via MassTransit Outbox (staged in DbContext ChangeTracker)
        var contractItems = orderItems.Select(x => new OrderItemDto(x.ProductId, x.Quantity, x.UnitPrice)).ToList();

        await _publishEndpoint.Publish(new OrderSubmittedEvent(
            order.Id,
            order.UserId,
            order.TotalAmount,
            contractItems,
            order.CreatedAt
        ), cancellationToken);

        // 3. Save BOTH Order entity and OutboxMessage in 1 single atomic DB transaction
        await _orderRepository.SaveChangesAsync(cancellationToken);

        var itemResponses = orderItems.Select(x => new OrderItemResponse(
            x.ProductId,
            x.ProductName,
            x.Quantity,
            x.UnitPrice,
            x.TotalPrice
        )).ToList();

        return new OrderResponse(
            order.Id,
            order.UserId,
            order.TotalAmount,
            order.Status.ToString(),
            order.CreatedAt,
            itemResponses
        );
    }
}
