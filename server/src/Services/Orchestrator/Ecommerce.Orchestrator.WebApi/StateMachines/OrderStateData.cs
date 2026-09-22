using MassTransit;

namespace Ecommerce.Orchestrator.WebApi.StateMachines;

public class OrderStateData : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = null!;

    public Guid UserId { get; set; }
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// What <see cref="TotalAmount"/> is denominated in (specs/022), carried from the submitted event
    /// to the payment command.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>This field exists because the saga RELAYS it, and a relay is where this project has
    /// already lost a field once.</b> When <c>OrderItemDto</c> gained <c>VariantId</c> (specs/020)
    /// every service was rebuilt except this one; it deserialised the event into its older record,
    /// dropped the field, and the wrong variant's stock moved. Dropping the currency would charge the
    /// right number in the wrong money, and nothing downstream could tell - 899 is a valid amount in
    /// both. Empty means the shop's default, which is what an Order built before specs/022 sends.
    /// </remarks>
    public string? Currency { get; set; }
    public Guid? PaymentId { get; set; }
    public string? FailureReason { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
