using Ecommerce.Order.Domain.Enums;

namespace Ecommerce.Order.Application.Orders.Common;

/// <summary>What happened when a part was asked to move one step (specs/035).</summary>
public enum ShipmentMoveOutcome
{
    /// <summary>It moved, and the order's summary was rewritten with it.</summary>
    Moved,

    /// <summary>It was already where it was asked to go - a repeat, answered as a no-op.</summary>
    AlreadyThere,

    /// <summary>It is somewhere the step does not start from. Nothing changed.</summary>
    WrongState,

    /// <summary>The order is not paid (still settling, or failed). Nothing changed.</summary>
    OrderNotPaid,

    /// <summary>No such order, or no part of this seller's on it. Nothing changed.</summary>
    NoSuchPart
}

/// <param name="Current">The part's status after the attempt, when there is a part.</param>
/// <param name="CurrentTracking">Its tracking reference, when shipped.</param>
/// <param name="OrderStatus">The order's status, when there is an order.</param>
public record ShipmentMoveResult(
    ShipmentMoveOutcome Outcome,
    ShipmentStatus? Current = null,
    string? CurrentTracking = null,
    OrderStatus? OrderStatus = null);
