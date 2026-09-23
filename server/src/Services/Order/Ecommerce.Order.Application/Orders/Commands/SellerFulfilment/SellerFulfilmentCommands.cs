using Ecommerce.Shared.Audit;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Commands.Fulfilment;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MediatR;

namespace Ecommerce.Order.Application.Orders.Commands.SellerFulfilment;

/// <summary>A seller starts preparing THEIR part of an order (specs/035).</summary>
/// <remarks>No seller id and no shipment id: the part is "mine, on this order", and the token says whose.</remarks>
public record PrepareMySaleCommand(Guid OrderId) : IRequest<SaleDetailResponse>;

/// <summary>A seller has sent their part, with the carrier's tracking reference.</summary>
public record ShipMySaleCommand(Guid OrderId, string TrackingReference) : IRequest<SaleDetailResponse>;

public class ShipMySaleCommandValidator : AbstractValidator<ShipMySaleCommand>
{
    public ShipMySaleCommandValidator()
    {
        // The same rule as the shop's own shipping - a tracking reference is what a customer follows.
        RuleFor(x => x.TrackingReference)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("A tracking reference of at most 100 characters is required to mark a part sent.");
    }
}

public class PrepareMySaleCommandHandler(IOrderRepository orders, ICurrentUser currentUser,
    IAuditTrail audit)
    : IRequestHandler<PrepareMySaleCommand, SaleDetailResponse>
{
    private readonly IAuditTrail _audit = audit;

    public Task<SaleDetailResponse> Handle(PrepareMySaleCommand request, CancellationToken cancellationToken) =>
        SellerStep.MoveAsync(orders, currentUser, request.OrderId,
            ShipmentStatus.Pending, ShipmentStatus.Preparing, null, cancellationToken, _audit);
}

public class ShipMySaleCommandHandler(IOrderRepository orders, ICurrentUser currentUser,
    IAuditTrail audit)
    : IRequestHandler<ShipMySaleCommand, SaleDetailResponse>
{
    private readonly IAuditTrail _audit = audit;

    public Task<SaleDetailResponse> Handle(ShipMySaleCommand request, CancellationToken cancellationToken) =>
        SellerStep.MoveAsync(orders, currentUser, request.OrderId,
            ShipmentStatus.Preparing, ShipmentStatus.Shipped, request.TrackingReference.Trim(), cancellationToken, _audit);
}

/// <summary>A seller's step on their own part, and what each outcome looks like from outside.</summary>
internal static class SellerStep
{
    public static async Task<SaleDetailResponse> MoveAsync(
        IOrderRepository orders,
        ICurrentUser currentUser,
        Guid orderId,
        ShipmentStatus from,
        ShipmentStatus to,
        string? trackingReference,
        CancellationToken cancellationToken,
        IAuditTrail? audit = null)
    {
        var sellerId = currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        var result = await orders.TryMoveShipmentAsync(
            orderId, sellerId, from, to, trackingReference, DateTime.UtcNow, cancellationToken,
            audit is null ? null : ct => ParcelAudit.RecordAsync(audit, orderId, sellerId, from, to, trackingReference, ct));

        switch (result.Outcome)
        {
            case ShipmentMoveOutcome.Moved:
            case ShipmentMoveOutcome.AlreadyThere:
                return await orders.GetSaleAsync(orderId, sellerId, cancellationToken)
                    ?? throw new NotFoundException(Sales.NotFound);

            // ⚠️ One answer for four situations, the specs/034 rule: no such order, nothing of theirs on
            // it, not paid yet, failed. Anything else would confirm that the order exists, or that
            // somebody else sold something on it.
            case ShipmentMoveOutcome.NoSuchPart:
            case ShipmentMoveOutcome.OrderNotPaid:
                throw new NotFoundException(Sales.NotFound);

            // Their own sale, cancelled (specs/039): they can see it, so saying so gives nothing away.
            case ShipmentMoveOutcome.OrderCancelled:
                throw new ConflictException(Sales.Cancelled);

            default: // WrongState - it IS theirs, so saying where it stands discloses nothing new
                if (result.Current == to)
                {
                    throw new ConflictException(
                        $"Your part is already {to} with tracking reference '{result.CurrentTracking}'.");
                }

                throw new ConflictException(
                    $"Your part is {FulfilmentStep.Describe(result.Current)}; only a "
                    + $"{FulfilmentStep.Describe(from)} part can become {to}.");
        }
    }
}
