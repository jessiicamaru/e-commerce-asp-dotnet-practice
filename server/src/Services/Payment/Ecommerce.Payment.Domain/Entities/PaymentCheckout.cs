namespace Ecommerce.Payment.Domain.Entities;

/// <summary>
/// An order waiting for its customer at a redirect gateway (specs/143): VNPay's page, then VNPay's signed call back. It
/// decides nothing - the <see cref="Payment"/> row is written only when the gateway has answered, exactly as the stub
/// writes it, so a payment stays immutable once written and its uniqueness per order keeps replays safe.
/// </summary>
public class PaymentCheckout
{
    public Guid Id { get; set; }

    /// <summary>One checkout per order: unique.</summary>
    public Guid OrderId { get; set; }

    /// <summary>The order's owner, from the saga's request - the only person who may read the pay link.</summary>
    public Guid UserId { get; set; }

    /// <summary>What the gateway must report back, to the unit - anything else is answered "invalid amount".</summary>
    public decimal Amount { get; set; }

    public string Currency { get; set; } = "";

    public string Provider { get; set; } = "";

    /// <summary>What the gateway calls the order (<c>vnp_TxnRef</c>): the order id in N format. Unique.</summary>
    public string Reference { get; set; } = "";

    public DateTime OpenedAt { get; set; }

    /// <summary>When the pay link stops working - shorter than the saga's wait for payment.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Set once, by the guarded claim of the gateway's answer; null while waiting.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>The gateway's answer code (<c>vnp_ResponseCode</c>).</summary>
    public string? ResponseCode { get; set; }

    /// <summary>The gateway's own transaction number (<c>vnp_TransactionNo</c>).</summary>
    public string? ProviderReference { get; set; }

    /// <summary>The reference a gateway is given for an order.</summary>
    public static string ReferenceFor(Guid orderId) => orderId.ToString("N");
}
