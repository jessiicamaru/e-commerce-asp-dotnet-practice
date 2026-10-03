namespace Ecommerce.Payment.Infrastructure.Gateway;

public class PaymentOutcomeOptions
{
    public const string SectionName = "Payment";

    /// <summary>
    /// What the stand-in produces: "Approve" or "Reject". Defaults to approving.
    /// <para>
    /// Rejecting exists so the saga's compensation branch — releasing held stock when payment
    /// fails — can actually be exercised. Until this service existed, that branch had never run.
    /// </para>
    /// </summary>
    public string Outcome { get; set; } = ApproveValue;

    /// <summary>Which provider takes payments (specs/143): "Stub", the default, or "VnPay" - <c>PAYMENT_PROVIDER</c>.</summary>
    public string Provider { get; set; } = StubProviderValue;

    public const string StubProviderValue = "Stub";
    public const string VnPayProviderValue = "VnPay";

    public const string ApproveValue = "Approve";
    public const string RejectValue = "Reject";
}
