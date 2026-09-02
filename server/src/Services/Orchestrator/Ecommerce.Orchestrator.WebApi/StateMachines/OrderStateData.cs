using MassTransit;

namespace Ecommerce.Orchestrator.WebApi.StateMachines;

public class OrderStateData : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = null!;

    public Guid UserId { get; set; }
    public decimal TotalAmount { get; set; }
    public Guid? PaymentId { get; set; }
    public string? FailureReason { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
