using Ecommerce.Payment.Application.Common.Interfaces;
using Ecommerce.Payment.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Ecommerce.Payment.Infrastructure.Gateway;

/// <summary>
/// The stand-in. It contacts nobody and moves no money — it reads a setting and reports an outcome.
/// <para>
/// This class is the seam. Replacing it with a real provider integration is the whole of what
/// "connecting payments" would mean; everything around it — one payment per order, the record and
/// its reply committing together, replay safety — already behaves as though money were real.
/// </para>
/// </summary>
public class StubPaymentGateway : IPaymentGateway
{
    private readonly PaymentStatus _configuredOutcome;

    public StubPaymentGateway(IOptions<PaymentOutcomeOptions> options)
    {
        var configured = options.Value.Outcome?.Trim();

        // Fail loudly rather than guessing. A typo silently treated as "Approve" would mean a
        // service someone believed was rejecting quietly approving everything instead.
        _configuredOutcome = configured switch
        {
            null or "" => PaymentStatus.Approved,
            _ when string.Equals(configured, PaymentOutcomeOptions.ApproveValue, StringComparison.OrdinalIgnoreCase)
                => PaymentStatus.Approved,
            _ when string.Equals(configured, PaymentOutcomeOptions.RejectValue, StringComparison.OrdinalIgnoreCase)
                => PaymentStatus.Rejected,
            _ => throw new InvalidOperationException(
                $"PAYMENT_OUTCOME is '{configured}', which is neither "
                + $"'{PaymentOutcomeOptions.ApproveValue}' nor '{PaymentOutcomeOptions.RejectValue}'.")
        };
    }

    public string ProviderName => Domain.Entities.Payment.StubProvider;

    public PaymentStatus ConfiguredOutcome => _configuredOutcome;

    public (PaymentStatus Status, string? FailureReason) Charge(Guid orderId, Guid userId, decimal amount)
    {
        return _configuredOutcome == PaymentStatus.Approved
            ? (PaymentStatus.Approved, null)
            : (PaymentStatus.Rejected,
               "Payment declined by the stub gateway (PAYMENT_OUTCOME=Reject)");
    }
}
