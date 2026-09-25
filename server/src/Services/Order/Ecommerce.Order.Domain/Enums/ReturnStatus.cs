namespace Ecommerce.Order.Domain.Enums;

/// <summary>
/// Where a return stands (specs/066). Stored as text.
/// <code>
/// Requested ─accept→ Accepted ─buyer sends→ SentBack ─received→ Received (refunded, restocked)
///     └─refuse→ Refused ─buyer escalates→ Escalated ─admin→ Accepted | Rejected
/// </code>
/// Rejected is final. An Accepted or Refused return the buyer does nothing about closes with its window.
/// </summary>
public enum ReturnStatus
{
    Requested = 1,
    Accepted = 2,
    Refused = 3,
    Escalated = 4,
    Rejected = 5,
    SentBack = 6,
    Received = 7,
}
