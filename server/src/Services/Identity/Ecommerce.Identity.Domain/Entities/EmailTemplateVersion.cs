namespace Ecommerce.Domain.Entities;

/// <summary>
/// One saved version of an email's words (specs/077): an administrator's edit, a reset to the built-in words, or
/// an earlier version restored. Append-only - the current words are the highest version, and undoing an edit adds
/// a version rather than rewriting one, so the history says who changed what, and back.
/// </summary>
public class EmailTemplateVersion
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>An <c>EmailTemplate</c> constant: <c>OrderPaid</c>, <c>PasswordReset</c>, <c>EmailConfirmation</c>.</summary>
    public string Template { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    /// <summary>1, 2, 3 ... per template and language; unique, which is what makes two saves at once produce one.</summary>
    public int Version { get; set; }

    /// <summary>True: back to the words in code. Subject and body are then null.</summary>
    public bool IsDefault { get; set; }

    public string? Subject { get; set; }

    /// <summary>Sanitised on save - and again when sent.</summary>
    public string? BodyHtml { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid CreatedBy { get; set; }
}
