namespace Ecommerce.Catalog.Domain.Entities;

/// <summary>What a shopper can report (specs/101).</summary>
public enum ReportTarget
{
    Review,
    Question,
    Product
}

/// <summary>Why, from a short list - a queue a moderator can sort, not a free-text inbox.</summary>
public enum ReportReason
{
    Spam,
    Offensive,
    Misleading,
    Counterfeit,
    Other
}

/// <summary>
/// Open until staff decide. <c>Actioned</c>: the thing was hidden or taken down, by whichever page staff used.
/// <c>Dismissed</c>: staff looked and left it as it is.
/// </summary>
public enum ReportStatus
{
    Open,
    Actioned,
    Dismissed
}

/// <summary>
/// A shopper saying a review, a question or a product should not be on the shop (#199, specs/101). One open report per
/// person per thing; staff close every open report of a thing at once, and each reporter is told how it ended.
/// </summary>
public class ContentReport
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public ReportTarget TargetType { get; set; }

    /// <summary>The review's, question's or product's id - not a foreign key, since it points at one of three tables.</summary>
    public Guid TargetId { get; set; }

    /// <summary>The product it hangs on (itself, for a product): the queue's link and name, and what a deletion cascades from.</summary>
    public Guid ProductId { get; set; }

    public Guid ReporterId { get; set; }

    public ReportReason Reason { get; set; }

    /// <summary>The reporter's own words, optional.</summary>
    public string? Details { get; set; }

    public ReportStatus Status { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedAt { get; set; }

    public Guid? ResolvedBy { get; set; }
}
