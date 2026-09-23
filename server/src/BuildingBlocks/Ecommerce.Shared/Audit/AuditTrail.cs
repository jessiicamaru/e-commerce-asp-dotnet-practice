using System.Text.Json;
using System.Text.Json.Nodes;
using Ecommerce.Contracts.Activity;
using Ecommerce.Shared.Authentication;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ecommerce.Shared.Audit;

/// <summary>The audit log's categories (specs/041) - how an administrator reads it by concern.</summary>
public static class AuditCategory
{
    public const string System = "System";
    public const string Security = "Security";
    public const string User = "User";
    public const string Catalog = "Catalog";
    public const string Order = "Order";
    public const string Payment = "Payment";
    public const string Moderation = "Moderation";

    public static readonly string[] All = [System, Security, User, Catalog, Order, Payment, Moderation];
}

/// <summary>Who did something, when it is not the caller - a sign-in, where nobody is signed in yet.</summary>
public record AuditActor(Guid? Id, string? Email, string? Role);

/// <summary>
/// Records one audit entry (specs/041).
/// </summary>
/// <remarks>
/// ⚠️ <b>Call it before the single <c>SaveChangesAsync</c></b>, like any publish through the outbox: the
/// entry then commits with the change it describes, or not at all (Principle III). Called after the save,
/// a crash in between leaves a change nobody can account for.
/// </remarks>
public interface IAuditTrail
{
    /// <param name="before">Anything serialisable; secrets are redacted. Null for a creation.</param>
    /// <param name="after">Anything serialisable; secrets are redacted. Null for a deletion.</param>
    /// <param name="actor">Overrides the caller - null means "whoever the token says", and no token the system.</param>
    Task RecordAsync(
        string category,
        string action,
        string subjectType,
        string? subjectId,
        string summary,
        object? before = null,
        object? after = null,
        AuditActor? actor = null,
        CancellationToken cancellationToken = default);
}

/// <remarks>
/// <see cref="ICurrentUser"/> is optional: a service or a test with no HTTP caller at all records as the
/// system, which is what a message consumer is.
/// </remarks>
public class AuditTrail(IPublishEndpoint publish, AuditTrailOptions options, ICurrentUser? currentUser = null) : IAuditTrail
{
    private readonly IPublishEndpoint _publish = publish;
    private readonly ICurrentUser? _currentUser = currentUser;
    private readonly AuditTrailOptions _options = options;

    /// <summary>Highest first: the role an entry is labelled with is the most powerful one held.</summary>
    private static readonly string[] Roles = ["Admin", "Moderator", "Seller", "Customer"];

    public Task RecordAsync(
        string category,
        string action,
        string subjectType,
        string? subjectId,
        string summary,
        object? before = null,
        object? after = null,
        AuditActor? actor = null,
        CancellationToken cancellationToken = default)
    {
        var who = actor ?? (_currentUser?.Id is not { } id
            ? new AuditActor(null, null, null)
            : new AuditActor(id, _currentUser.Email, Roles.FirstOrDefault(_currentUser.IsInRole)));

        return _publish.Publish(new AuditEntryRecorded(
            Guid.CreateVersion7(),
            category,
            action,
            who.Id,
            who.Email,
            who.Role,
            subjectType,
            subjectId,
            summary,
            AuditSnapshot.Serialize(before),
            AuditSnapshot.Serialize(after),
            _options.ServiceName,
            DateTime.UtcNow), cancellationToken);
    }
}

public record AuditTrailOptions(string ServiceName);

/// <summary>Snapshots as JSON, with every secret-looking value replaced before it leaves the service.</summary>
public static class AuditSnapshot
{
    public const string Redacted = "***";

    private static readonly string[] SecretWords = ["password", "token", "secret", "hash"];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string? Serialize(object? value)
    {
        if (value is null)
        {
            return null;
        }

        var node = JsonSerializer.SerializeToNode(value, value.GetType(), Json);
        Redact(node);
        return node?.ToJsonString(Json);
    }

    /// <summary>Whether a property of this name must never be stored.</summary>
    public static bool IsSecret(string name) =>
        SecretWords.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase));

    private static void Redact(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(p => p.Key).ToList())
                {
                    if (IsSecret(key))
                    {
                        obj[key] = Redacted;
                    }
                    else
                    {
                        Redact(obj[key]);
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    Redact(item);
                }

                break;
        }
    }
}

public static class AuditTrailRegistration
{
    /// <summary>Registers <see cref="IAuditTrail"/> for a service. Needs <c>AddJwtAuthentication</c> for the actor.</summary>
    public static IServiceCollection AddAuditTrail(this IServiceCollection services, string serviceName)
    {
        services.TryAddSingleton(new AuditTrailOptions(serviceName));
        services.TryAddScoped<IAuditTrail, AuditTrail>();
        return services;
    }
}
