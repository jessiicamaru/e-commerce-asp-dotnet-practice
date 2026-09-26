using Ecommerce.Contracts.Activity;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ecommerce.Shared.Notifications;

/// <summary>What a notification can be about (specs/042). The storefront has words for each.</summary>
public static class NotificationKind
{
    // A customer's order
    public const string OrderPaid = "OrderPaid";
    public const string OrderFailed = "OrderFailed";
    public const string ParcelShipped = "ParcelShipped";
    public const string OrderCancelled = "OrderCancelled";

    // A seller's sales and money
    public const string NewSale = "NewSale";
    public const string SaleCancelled = "SaleCancelled";
    public const string ParcelReceived = "ParcelReceived";
    public const string PayoutRecorded = "PayoutRecorded";
    public const string ParcelAutoDelivered = "ParcelAutoDelivered";

    // Somebody's account (specs/043)
    public const string ModeratorGranted = "ModeratorGranted";
    public const string ModeratorRevoked = "ModeratorRevoked";

    // What staff did to somebody's account or words (specs/059)
    public const string AccountLocked = "AccountLocked";
    public const string AccountBanned = "AccountBanned";
    public const string ReviewHidden = "ReviewHidden";

    // Somebody's application to sell (specs/044)
    public const string ShopApproved = "ShopApproved";
    public const string ShopRejected = "ShopRejected";

    // A seller's product and the moderators (specs/045)
    public const string ProductApproved = "ProductApproved";
    public const string ProductRejected = "ProductRejected";
    public const string ProductTakenDown = "ProductTakenDown";

    // Somebody reviewed a seller's product (specs/046)
    public const string NewReview = "NewReview";

    // A delivered parcel sent back (specs/066)
    public const string ReturnRequested = "ReturnRequested";
    public const string ReturnAccepted = "ReturnAccepted";
    public const string ReturnRefused = "ReturnRefused";
    public const string ReturnSentBack = "ReturnSentBack";
    public const string ReturnRefunded = "ReturnRefunded";

    // A saved product came back in stock (specs/075)
    public const string SavedBackInStock = "SavedBackInStock";
}

/// <summary>
/// Tells one person something (specs/042).
/// </summary>
/// <remarks>
/// ⚠️ Like <c>IAuditTrail</c>, this publishes through the caller's outbox: call it before the one
/// <c>SaveChangesAsync</c>, or inside a repository's <c>stage</c>, so the notice commits with the change it
/// announces. A notice about an order that then failed to save would be a lie in somebody's inbox.
/// </remarks>
public interface INotifier
{
    Task NotifyAsync(
        Guid recipientId,
        string kind,
        IReadOnlyDictionary<string, string>? data = null,
        string? link = null,
        CancellationToken cancellationToken = default);
}

public class Notifier(IPublishEndpoint publish) : INotifier
{
    private readonly IPublishEndpoint _publish = publish;

    public Task NotifyAsync(
        Guid recipientId,
        string kind,
        IReadOnlyDictionary<string, string>? data = null,
        string? link = null,
        CancellationToken cancellationToken = default) =>
        _publish.Publish(new UserNotificationRequested(
            Guid.CreateVersion7(),
            recipientId,
            kind,
            data is null ? [] : new Dictionary<string, string>(data),
            link,
            DateTime.UtcNow), cancellationToken);
}

public static class NotifierRegistration
{
    public static IServiceCollection AddNotifier(this IServiceCollection services)
    {
        services.TryAddScoped<INotifier, Notifier>();
        return services;
    }
}
