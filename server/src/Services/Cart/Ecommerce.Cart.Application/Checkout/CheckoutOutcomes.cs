using System.Text.Json;
using System.Text.Json.Serialization;
using Ecommerce.Contracts.Order;
using Ecommerce.Cart.Application.Common.Interfaces;
using Ecommerce.Cart.Domain.Entities;

namespace Ecommerce.Cart.Application.Checkout;

/// <summary>
/// Removes what an order bought from the customer's cart - once, and only when the order completes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why completion and not submission.</b> The saga's Failed is terminal: there is no retrying a
/// payment. Removing lines at submission would leave a customer whose card was declined with an empty
/// cart and a dead order. Waiting for completion leaves their cart intact, and they check out again.
/// </para>
/// <para>
/// <b>Why three events.</b> OrderCompletedEvent carries only an order id. The items and the customer
/// are on OrderSubmittedEvent, so this remembers them until completion arrives.
/// </para>
/// <para>
/// <b>Why either order must work.</b> Nothing orders delivery across message types. If only the likely
/// order were handled, a completion arriving first would do nothing and the late submission would then
/// record items that nothing ever applied - the cart would never clear, and nothing would say so. That
/// is the silent shape of issue #15, and it happened in this repository. So both RecordSubmitted and
/// RecordCompleted check whether the other has already arrived, and whichever is second applies.
/// </para>
/// <para>
/// <b>Why once.</b> Every event is handled under a row lock on the order's record, and the removal
/// happens only while Applied is false, in the same transaction that sets it. A redelivery changes
/// nothing - a guarded state transition in the database, as the constitution requires, not a
/// configuration flag.
/// </para>
/// </remarks>
public class CheckoutOutcomes(
    ICheckoutOutcomeRepository outcomes,
    ICartRepository carts,
    IUnitOfWork unitOfWork)
{
    private readonly ICheckoutOutcomeRepository _outcomes = outcomes;
    private readonly ICartRepository _carts = carts;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public Task RecordSubmittedAsync(
        Guid orderId,
        Guid userId,
        IReadOnlyList<OrderedItem> items,
        CancellationToken cancellationToken = default)
        => _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var outcome = await _outcomes.GetOrCreateForUpdateAsync(orderId, ct);

            outcome.UserId = userId;
            outcome.ItemsJson = JsonSerializer.Serialize(items);
            outcome.UpdatedAt = DateTime.UtcNow;

            // Completion may already have arrived. If so, this is the second event, and it applies.
            await TryApplyAsync(outcome, ct);
            await _carts.SaveChangesAsync(ct);
        }, cancellationToken);

    public Task RecordCompletedAsync(Guid orderId, CancellationToken cancellationToken = default)
        => _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var outcome = await _outcomes.GetOrCreateForUpdateAsync(orderId, ct);

            outcome.Outcome = CheckoutOutcomeStatus.Completed;
            outcome.UpdatedAt = DateTime.UtcNow;

            // Submission may not have arrived yet. If not, there is nothing to apply, and the
            // submission will apply it when it comes.
            await TryApplyAsync(outcome, ct);
            await _carts.SaveChangesAsync(ct);
        }, cancellationToken);

    public Task RecordFailedAsync(Guid orderId, CancellationToken cancellationToken = default)
        => _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var outcome = await _outcomes.GetOrCreateForUpdateAsync(orderId, ct);

            // A failed order never touches the cart. Only a still-pending record is marked: an order
            // cannot both complete and fail, and a stray failure must not undo a completion.
            if (outcome.Outcome == CheckoutOutcomeStatus.Pending)
            {
                outcome.Outcome = CheckoutOutcomeStatus.Failed;
                outcome.UpdatedAt = DateTime.UtcNow;
            }

            await _carts.SaveChangesAsync(ct);
        }, cancellationToken);

    private async Task TryApplyAsync(CheckoutOutcome outcome, CancellationToken ct)
    {
        if (outcome.Applied
            || outcome.Outcome != CheckoutOutcomeStatus.Completed
            || outcome.UserId is null
            || outcome.ItemsJson is null)
        {
            return;
        }

        var items = JsonSerializer.Deserialize<List<OrderedItem>>(outcome.ItemsJson) ?? [];
        var cart = await _carts.GetForUpdateAsync(outcome.UserId.Value, ct);

        if (cart is not null)
        {
            foreach (var item in items)
            {
                // The VARIANT that was bought (#122): by product, two shapes of one lens in the cart meant
                // the decrement could land on the one that was not.
                var line = cart.Lines.FirstOrDefault(l => l.SellableId == item.Sellable);

                if (line is null)
                {
                    // Removed by the customer during checkout. Nothing to do.
                    continue;
                }

                // A DECREMENT of what was ordered, never "empty the cart". Anything the customer added
                // while the order was in flight - a new product, or a raised quantity - survives.
                line.Quantity -= item.Quantity;

                if (line.Quantity <= 0)
                {
                    _carts.RemoveLine(line);
                }
            }

            cart.UpdatedAt = DateTime.UtcNow;
        }

        outcome.Applied = true;
    }
}

/// <summary>One line of an order, as Cart remembers it until the order completes.</summary>
/// <param name="VariantId">
/// What was bought (specs/020, #122). <c>Guid.Empty</c> - an item stored before specs/052, or sent by an
/// Order older than specs/020 - means the product's first variant, whose id IS the product id.
/// </param>
public record OrderedItem(Guid ProductId, int Quantity, Guid VariantId = default)
{
    /// <summary>What to match a cart line's <see cref="CartLine.SellableId"/> against.</summary>
    [JsonIgnore]
    public Guid Sellable => VariantId == Guid.Empty ? ProductId : VariantId;

    /// <summary>The one mapping from the event - where the variant used to be dropped.</summary>
    public static OrderedItem From(OrderItemDto item) => new(item.ProductId, item.Quantity, item.VariantId);
}
