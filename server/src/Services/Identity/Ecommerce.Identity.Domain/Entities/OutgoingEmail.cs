namespace Ecommerce.Domain.Entities;

/// <summary>
/// An email somebody asked Identity to send (specs/060), kept until it is sent - or until it is clear it
/// cannot be. A mail server that is down delays it; nothing loses it.
/// </summary>
public class OutgoingEmail
{
    /// <summary>The requester's id for this email: a redelivered request finds it and adds nothing.</summary>
    public Guid Id { get; set; }

    public Guid RecipientId { get; set; }

    public string Template { get; set; } = string.Empty;

    /// <summary>The template's data, as JSON - rendered when it is sent, in <see cref="Language"/>.</summary>
    public string DataJson { get; set; } = "{}";

    public string Language { get; set; } = string.Empty;

    public OutgoingEmailStatus Status { get; set; } = OutgoingEmailStatus.Pending;

    public int Attempts { get; set; }

    /// <summary>Not before this. Pushed back after each failed attempt.</summary>
    public DateTime NextAttemptAt { get; set; }

    public DateTime? SentAt { get; set; }

    /// <summary>Why the last attempt failed - kept, so an email that gave up says why.</summary>
    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; }
}

public enum OutgoingEmailStatus
{
    Pending,
    Sent,
    Failed,
}
