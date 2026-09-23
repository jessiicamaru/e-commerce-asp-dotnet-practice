namespace Ecommerce.Activity.Domain.Entities;

/// <summary>One thing somebody was told (specs/042) - a kind and its data, worded by the storefront.</summary>
public class Notification
{
    /// <summary>Minted by the publisher: a redelivered message is the same notification.</summary>
    public Guid Id { get; set; }

    public Guid RecipientId { get; set; }
    public string Kind { get; set; } = string.Empty;

    /// <summary>JSON object of strings - what the words need.</summary>
    public string Data { get; set; } = "{}";

    public string? Link { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Null until the recipient has seen it.</summary>
    public DateTime? ReadAt { get; set; }
}
