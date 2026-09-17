using MediatR;

namespace Ecommerce.Order.Application.Orders.Commands.FailOrder;

/// <summary>
/// Records that an order's checkout failed, and why.
/// </summary>
/// <param name="Reason">
/// Whatever failed the checkout said, verbatim. Two different sources reach here: a stock
/// reservation that could not be met, and a rejected payment. Neither is translated into
/// shopper-facing wording — that is a separate concern.
/// </param>
/// <remarks>
/// Returns whether this call settled the order. <c>false</c> means it had already settled, or is not
/// held here.
/// </remarks>
public record FailOrderCommand(
    Guid OrderId,
    string Reason,
    DateTime FailedAt
) : IRequest<bool>;
