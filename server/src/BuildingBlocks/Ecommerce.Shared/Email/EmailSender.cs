using Ecommerce.Contracts.Identity;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ecommerce.Shared.Email;

/// <summary>What an email can be (specs/060). Identity has a template in each language for each.</summary>
public static class EmailTemplate
{
    /// <summary>An order settled as paid. Data: <c>orderId</c>, <c>total</c>, <c>currency</c>.</summary>
    public const string OrderPaid = "OrderPaid";

    /// <summary>
    /// A link to choose a new password (specs/061). Data: <c>token</c> - queued by Identity itself, never through
    /// the broker, and scrubbed from the row once sent.
    /// </summary>
    public const string PasswordReset = "PasswordReset";

    /// <summary>
    /// A link to confirm an address belongs to its account (specs/063). Data: <c>token</c> - queued by Identity
    /// itself, never through the broker, and scrubbed from the row once sent.
    /// </summary>
    public const string EmailConfirmation = "EmailConfirmation";

    /// <summary>A parcel of an order left (specs/083). Data: <c>orderId</c>, <c>tracking</c>, and <c>shop</c> for a seller's.</summary>
    public const string ParcelShipped = "ParcelShipped";

    /// <summary>A paid order was cancelled and will be refunded in full (specs/083). Data: <c>orderId</c>, <c>total</c>, <c>currency</c>.</summary>
    public const string OrderCancelled = "OrderCancelled";

    /// <summary>A return was accepted - send the parcel back (specs/083). Data: <c>orderId</c>.</summary>
    public const string ReturnAccepted = "ReturnAccepted";

    /// <summary>A return was refused, by the seller or finally by the shop (specs/083). Data: <c>orderId</c>, <c>reason</c>.</summary>
    public const string ReturnRefused = "ReturnRefused";

    /// <summary>A returned parcel came back and its refund is on the way (specs/083). Data: <c>orderId</c>, <c>amount</c>, <c>currency</c>.</summary>
    public const string ReturnRefunded = "ReturnRefunded";

    /// <summary>A product the reader saved is back in stock (specs/083). Data: <c>productId</c>, <c>product</c>.</summary>
    public const string SavedBackInStock = "SavedBackInStock";

    /// <summary>The reader's account was locked (specs/083). Data: <c>until</c> (ISO 8601, UTC), <c>reason</c>.</summary>
    public const string AccountLocked = "AccountLocked";

    /// <summary>The reader's account was banned (specs/083). Data: <c>reason</c>.</summary>
    public const string AccountBanned = "AccountBanned";

    /// <summary>
    /// The language to ask for when the sender does not know the reader's (specs/083): Identity writes the email in
    /// the language the person last used the shop in, or the default.
    /// </summary>
    public const string ReadersLanguage = "";
}

/// <summary>
/// Emails one person (specs/060).
/// </summary>
/// <remarks>
/// ⚠️ Like <c>INotifier</c> and <c>IAuditTrail</c>, this publishes through the caller's outbox: call it before
/// the one <c>SaveChangesAsync</c>, or inside a repository's <c>stage</c>, so the email commits with the
/// change it is about. It does not wait for a mail server - Identity queues it and sends it when it can.
/// </remarks>
public interface IEmailSender
{
    Task SendAsync(
        Guid recipientId,
        string template,
        IReadOnlyDictionary<string, string> data,
        string language,
        CancellationToken cancellationToken = default);
}

public class EmailSender(IPublishEndpoint publish) : IEmailSender
{
    private readonly IPublishEndpoint _publish = publish;

    public Task SendAsync(
        Guid recipientId,
        string template,
        IReadOnlyDictionary<string, string> data,
        string language,
        CancellationToken cancellationToken = default) =>
        _publish.Publish(new EmailRequested(
            Guid.CreateVersion7(),
            recipientId,
            template,
            new Dictionary<string, string>(data),
            language,
            DateTime.UtcNow), cancellationToken);
}

public static class EmailSenderRegistration
{
    public static IServiceCollection AddEmailSender(this IServiceCollection services)
    {
        services.TryAddScoped<IEmailSender, EmailSender>();
        return services;
    }
}
