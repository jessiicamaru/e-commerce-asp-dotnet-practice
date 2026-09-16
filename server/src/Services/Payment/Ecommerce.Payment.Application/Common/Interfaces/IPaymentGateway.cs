using Ecommerce.Payment.Domain.Enums;

namespace Ecommerce.Payment.Application.Common.Interfaces;

/// <summary>
/// Decides the outcome of a payment. The seam a real provider integration would replace — today the
/// only implementation reads a configured setting and contacts nobody.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>The name recorded on every payment, so a row states what produced its outcome.</summary>
    string ProviderName { get; }

    /// <summary>The outcome this gateway is currently configured to produce, for health reporting.</summary>
    PaymentStatus ConfiguredOutcome { get; }

    (PaymentStatus Status, string? FailureReason) Charge(Guid orderId, Guid userId, decimal amount);
}
