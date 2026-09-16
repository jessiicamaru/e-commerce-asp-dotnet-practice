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

    public const string ApproveValue = "Approve";
    public const string RejectValue = "Reject";
}
