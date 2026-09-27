namespace Ecommerce.Contracts.Identity;

/// <summary>
/// A seller's shop was closed or reopened (#193, specs/095): their account was banned, or the ban lifted.
/// </summary>
/// <remarks>
/// <para>
/// Published by Identity through its outbox in the ban's own transaction, only for somebody who holds the Seller role.
/// Catalog consumes it and takes the seller's products off the shelf, or puts them back as they were.
/// </para>
/// <para>
/// One message with a flag, not two: suspended and reinstated can arrive in either order, and one message type shares
/// one ordering guard (<paramref name="ChangedAt"/>) and one consumer queue. A lock does not close a shop (specs/095 D1).
/// </para>
/// </remarks>
public record SellerSuspensionChangedEvent(
    Guid SellerId,
    bool Suspended,
    DateTime ChangedAt
);
