using MediatR;

namespace Ecommerce.Order.Application.Orders.Commands.CompleteOrder;

/// <summary>
/// Records that an order's checkout finished successfully.
/// </summary>
/// <param name="CompletedAt">
/// The orchestrator's clock, carried on the announcement. It is recorded as sent rather than
/// replaced with local time, so the two services agree on when the order finished.
/// </param>
/// <remarks>
/// Returns whether this call was the one that settled the order. <c>false</c> is an ordinary
/// outcome — the order had already settled, or is not held here.
/// </remarks>
public record CompleteOrderCommand(
    Guid OrderId,
    DateTime CompletedAt
) : IRequest<bool>;
