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
