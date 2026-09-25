namespace Ecommerce.Contracts.Identity;

/// <summary>
/// Send somebody an email (specs/060). Published by any service through its outbox, so an email about a
/// change commits with the change; consumed by Identity - the one service that knows email addresses.
/// </summary>
/// <remarks>
/// Like a notification (specs/042) it carries a template and data, never a sentence: Identity renders it in
/// <paramref name="Language"/> when it sends, and the recipient's address and name are read there, from the
/// account, rather than copied into every service that wants to write to somebody.
/// </remarks>
/// <param name="EmailId">Identity keeps each request once, by this id - a redelivered message sends nothing twice.</param>
/// <param name="Language">What the email is written in - for an order, the language it was placed in.</param>
public record EmailRequested(
    Guid EmailId,
    Guid RecipientId,
    string Template,
    Dictionary<string, string> Data,
    string Language,
    DateTime RequestedAt
);
