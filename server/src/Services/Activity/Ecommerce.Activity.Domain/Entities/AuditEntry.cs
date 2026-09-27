namespace Ecommerce.Activity.Domain.Entities;

/// <summary>
/// One thing somebody - or the system - did, as the service that did it reported it (specs/041).
/// Written once, never changed: an audit log that can be edited is not one.
/// </summary>
public class AuditEntry
{
    /// <summary>Minted by the publisher, so a redelivered message is the same entry.</summary>
    public Guid Id { get; set; }

    public string Category { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;

    /// <summary>Null: the system did it - a sweeper, a message, nobody signed in.</summary>
    public Guid? ActorId { get; set; }
    public string? ActorEmail { get; set; }
    public string? ActorRole { get; set; }

    public string SubjectType { get; set; } = string.Empty;
    public string? SubjectId { get; set; }
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// The person the entry is about (specs/100): the user locked, the author of a review hidden, the seller of a product
    /// taken down. Null for entries about nobody in particular, and for content decisions recorded before the field.
    /// </summary>
    public Guid? AboutUserId { get; set; }

    /// <summary>JSON snapshots, secrets already redacted by the publisher.</summary>
    public string? Before { get; set; }
    public string? After { get; set; }

    /// <summary>JSON array of changed fields, computed once when recorded (research D5).</summary>
    public string Changes { get; set; } = "[]";
    public int ChangeCount { get; set; }

    public string Service { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public DateTime RecordedAt { get; set; }
}
