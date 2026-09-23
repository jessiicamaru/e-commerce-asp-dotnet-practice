namespace Ecommerce.Contracts.Activity;

/// <summary>
/// Somebody should be told something (specs/042). Published by the service where it happened, through its
/// outbox, in the same transaction; the Activity service puts it in the recipient's inbox.
/// </summary>
/// <param name="NotificationId">Minted by the publisher and the notification's key: a redelivery is one notification.</param>
/// <param name="Kind">What happened, e.g. <c>OrderPaid</c>. The storefront words it in the reader's language.</param>
/// <param name="Data">What the words need - an order id, an amount - as strings. Never a sentence.</param>
/// <param name="Link">Where it points in the storefront, e.g. <c>/orders/{id}</c>.</param>
public record UserNotificationRequested(
    Guid NotificationId,
    Guid RecipientId,
    string Kind,
    Dictionary<string, string> Data,
    string? Link,
    DateTime OccurredAt);
