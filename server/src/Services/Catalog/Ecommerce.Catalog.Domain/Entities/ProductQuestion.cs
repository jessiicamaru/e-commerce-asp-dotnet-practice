namespace Ecommerce.Catalog.Domain.Entities;

/// <summary>
/// A shopper's question about a product and its seller's one answer (specs/076). Each part is hidden - never
/// deleted - by staff on its own: a hidden question leaves the page with its answer, a hidden answer leaves the
/// question reading as unanswered.
/// </summary>
public class ProductQuestion
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ProductId { get; set; }

    public Guid AskerId { get; set; }

    /// <summary>Their first name when they asked, from the token - never from the request, as a review's is.</summary>
    public string AskerName { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? HiddenAt { get; set; }

    public string? HiddenReason { get; set; }

    public Guid? HiddenBy { get; set; }

    /// <summary>The seller's answer - or staff's, for the shop's own product. One per question; a rewrite replaces it.</summary>
    public string? Answer { get; set; }

    public Guid? AnsweredBy { get; set; }

    /// <summary>The FIRST answer: what the asker was told about. A rewrite moves <see cref="AnswerUpdatedAt"/> only.</summary>
    public DateTime? AnsweredAt { get; set; }

    public DateTime? AnswerUpdatedAt { get; set; }

    public DateTime? AnswerHiddenAt { get; set; }

    public string? AnswerHiddenReason { get; set; }

    public Guid? AnswerHiddenBy { get; set; }
}
