using Ecommerce.Payment.Domain.Enums;

namespace Ecommerce.Payment.Application.Common.Interfaces;

/// <summary>
/// The seam a payment provider plugs into. The stub decides at once; a redirect gateway such as VNPay (specs/143) asks
/// for the customer instead, and decides when its signed notification arrives.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>The name recorded on every payment, so a row states what produced its outcome.</summary>
    string ProviderName { get; }

    /// <summary>How health and the startup log describe it: "Stub", "VnPay sandbox", "VnPay".</summary>
    string Description { get; }

    /// <summary>
    /// Whether real money moves. False for the stub and for VNPay's sandbox - one of the signals that a stand-in is never
    /// taken for the real thing, read by the storefront's payment notice through <c>/health</c>.
    /// </summary>
    bool MovesMoney { get; }

    /// <summary>What health reports as <c>configuredOutcome</c>: "Approve" or "Reject" for the stub, "Customer" for a redirect gateway.</summary>
    string HealthOutcome { get; }

    GatewayDecision Begin(Guid orderId, Guid userId, decimal amount, string currency);
}

/// <summary>Either an outcome now, or "the customer pays at the gateway, within this window".</summary>
public sealed record GatewayDecision(PaymentStatus? Status, string? FailureReason, TimeSpan? PaymentWindow)
{
    public bool AwaitsCustomer => PaymentWindow is not null;

    public static GatewayDecision Decided(PaymentStatus status, string? failureReason = null) => new(status, failureReason, null);

    public static GatewayDecision AwaitCustomer(TimeSpan window) => new(null, null, window);
}
