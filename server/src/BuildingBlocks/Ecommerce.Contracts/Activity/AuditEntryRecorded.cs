namespace Ecommerce.Contracts.Activity;

/// <summary>
/// Something worth answering "who did this" about happened (specs/041). Published by the service that
/// made the change, through its outbox, in the same transaction; the Activity service keeps it.
/// </summary>
/// <param name="EntryId">Minted by the publisher and the entry's key, so a redelivery is recorded once.</param>
/// <param name="Category">One of <c>AuditCategory</c>: System, Security, User, Catalog, Order, Payment, Moderation.</param>
/// <param name="Action">What happened, in PascalCase: <c>ProductUpdated</c>, <c>PayoutRecorded</c>.</param>
/// <param name="ActorId">Who did it; null when the system did (a sweeper, a message).</param>
/// <param name="ActorRole">The most powerful role the actor held: Admin, Moderator, Seller or Customer.</param>
/// <param name="SubjectType">What it was done to: <c>Product</c>, <c>Order</c>, <c>User</c>...</param>
/// <param name="Before">JSON snapshot before the change, secrets redacted; null for a creation.</param>
/// <param name="After">JSON snapshot after the change, secrets redacted; null for a deletion.</param>
/// <param name="Service">Which service recorded it.</param>
public record AuditEntryRecorded(
    Guid EntryId,
    string Category,
    string Action,
    Guid? ActorId,
    string? ActorEmail,
    string? ActorRole,
    string SubjectType,
    string? SubjectId,
    string Summary,
    string? Before,
    string? After,
    string Service,
    DateTime OccurredAt);
