namespace Ecommerce.Activity.Domain.Entities;

/// <summary>
/// One saved version of a notification's words (specs/078): an administrator's edit, a reset to the storefront's
/// bundled words, or an earlier version restored. Append-only, like the emails' (specs/077): the current words are
/// the highest version.
/// </summary>
public class NotificationWordingVersion
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>A notification kind, or a kind with an i18next plural suffix: <c>NewReview_one</c>.</summary>
    public string Key { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public int Version { get; set; }

    /// <summary>True: back to the words bundled with the storefront. <see cref="Text"/> is then null.</summary>
    public bool IsDefault { get; set; }

    /// <summary>With <c>{{placeholders}}</c>; sanitised on save, and again where it is shown.</summary>
    public string? Text { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid CreatedBy { get; set; }
}
