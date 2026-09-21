namespace Ecommerce.Cart.Domain.Entities;

/// <summary>
/// The cart's own record of one checkout, assembled from three events.
/// </summary>
/// <remarks>
/// <para>
/// It exists because <c>OrderCompletedEvent</c> carries only an order id - no items, no user - so
/// the cart cannot know what to remove from the completion alone. It keeps what it learned from the
/// submission and applies it on completion.
/// </para>
/// <para>
/// <b>The events can arrive in either order.</b> Nothing orders delivery across message types, and
/// this repository has already been bitten by exactly that (issue #15). So whichever of submission
/// and completion arrives second applies the removal, and <see cref="Applied"/> makes it happen once.
/// </para>
/// </remarks>
public class CheckoutOutcome
{
    public Guid OrderId { get; set; }

    /// <summary>Known once the submission arrives.</summary>
    public Guid? UserId { get; set; }

    /// <summary>What the order contained, as JSON. Known once the submission arrives.</summary>
    public string? ItemsJson { get; set; }

    public CheckoutOutcomeStatus Outcome { get; set; }

    /// <summary>The guard. The lines are removed once, ever - a redelivery changes nothing.</summary>
    public bool Applied { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public enum CheckoutOutcomeStatus
{
    Pending,
    Completed,
    Failed
}
