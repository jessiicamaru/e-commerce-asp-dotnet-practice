namespace Ecommerce.Payment.Infrastructure.Gateway;

/// <summary>
/// VNPay's settings (specs/143). The merchant code and hash secret are the merchant's, registered with VNPay and never
/// committed; in development and CI they are the simulator's.
/// </summary>
public class VnPayOptions
{
    public const string SectionName = "VnPay";

    public const string SandboxPayUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";

    /// <summary><c>vnp_TmnCode</c> - <c>VNPAY_TMN_CODE</c>.</summary>
    public string? TmnCode { get; set; }

    /// <summary>The HMAC-SHA512 key - <c>VNPAY_HASH_SECRET</c>.</summary>
    public string? HashSecret { get; set; }

    /// <summary>Where the customer is sent to pay - VNPay's sandbox by default, the simulator in development.</summary>
    public string PayUrl { get; set; } = SandboxPayUrl;

    /// <summary>Where the gateway sends the customer back: the storefront's return page.</summary>
    public string? ReturnUrl { get; set; }

    /// <summary>How long the pay link works (<c>vnp_ExpireDate</c>) - shorter than the saga's wait for payment.</summary>
    public int PaymentWindowMinutes { get; set; } = 8;

    /// <summary>
    /// The production gateway: rows say "VnPay" and health says money moves. False - the sandbox, or the simulator -
    /// unless set on purpose.
    /// </summary>
    public bool Live { get; set; }

    /// <summary>What would make VNPay fail later, said at startup instead.</summary>
    /// <param name="sagaPaymentTimeoutSeconds">The orchestrator's wait, when this service is told it.</param>
    public IReadOnlyList<string> Problems(int? sagaPaymentTimeoutSeconds)
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(TmnCode))
            problems.Add("VNPAY_TMN_CODE is empty.");
        if (string.IsNullOrWhiteSpace(HashSecret))
            problems.Add("VNPAY_HASH_SECRET is empty.");
        if (!Uri.TryCreate(PayUrl, UriKind.Absolute, out _))
            problems.Add($"VNPAY_PAY_URL '{PayUrl}' is not an absolute address.");
        if (!Uri.TryCreate(ReturnUrl, UriKind.Absolute, out _))
            problems.Add($"VNPAY_RETURN_URL '{ReturnUrl}' is not an absolute address (or set STOREFRONT_URL).");
        if (PaymentWindowMinutes is < 1 or > 60)
            problems.Add($"VNPAY_PAYMENT_WINDOW_MINUTES {PaymentWindowMinutes} must be from 1 to 60.");
        else if (sagaPaymentTimeoutSeconds is { } timeout && PaymentWindowMinutes * 60 >= timeout)
            problems.Add($"VNPAY_PAYMENT_WINDOW_MINUTES ({PaymentWindowMinutes} min) must be shorter than the saga's "
                + $"ORCHESTRATOR_PAYMENT_TIMEOUT_SECONDS ({timeout} s), or a customer could pay for an order that has already failed.");
        if (Live && PayUrl == SandboxPayUrl)
            problems.Add("VNPAY_LIVE is true but VNPAY_PAY_URL is the sandbox.");
        return problems;
    }
}
