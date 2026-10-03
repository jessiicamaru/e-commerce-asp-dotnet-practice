using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Ecommerce.Payment.Application.Common.Interfaces;
using Ecommerce.Payment.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Ecommerce.Payment.Infrastructure.Gateway;

/// <summary>
/// VNPay 2.1.0's signing (specs/143 research D3): every non-empty <c>vnp_*</c> parameter but the hash itself, sorted by
/// key (ordinal), URL-encoded as VNPay's own .NET sample does, joined with <c>&amp;</c>, and HMAC-SHA512'd with the hash
/// secret. Unconfigured, it builds no link and believes no notification.
/// </summary>
public class VnPaySignature(IOptions<VnPayOptions> options) : IVnPay
{
    public const string Version = "2.1.0";

    private readonly VnPayOptions _options = options.Value;

    /// <summary>Vietnam keeps no daylight saving: its clock is UTC+7 all year, which is what VNPay's dates are written in.</summary>
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public string BuildPayUrl(PaymentCheckout checkout, string ipAddress, string language, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(_options.TmnCode) || string.IsNullOrWhiteSpace(_options.HashSecret))
        {
            throw new InvalidOperationException("VNPay is not configured: VNPAY_TMN_CODE and VNPAY_HASH_SECRET are required.");
        }

        var parameters = new Dictionary<string, string>
        {
            ["vnp_Version"] = Version,
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = _options.TmnCode,
            // In hundredths: VNPay's amounts have two implied decimals even for dong, which has none.
            ["vnp_Amount"] = ((long)decimal.Round(checkout.Amount * 100m, 0)).ToString(CultureInfo.InvariantCulture),
            ["vnp_CurrCode"] = "VND",
            ["vnp_TxnRef"] = checkout.Reference,
            // Plain ASCII: VNPay asks for no diacritics in the order description.
            ["vnp_OrderInfo"] = $"Order {checkout.Reference}",
            ["vnp_OrderType"] = "other",
            ["vnp_Locale"] = language == "en" ? "en" : "vn",
            ["vnp_ReturnUrl"] = _options.ReturnUrl ?? "",
            ["vnp_IpAddr"] = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress,
            ["vnp_CreateDate"] = VietnamTime(now),
            // Never later than the checkout's own expiry, however long ago it was opened.
            ["vnp_ExpireDate"] = VietnamTime(checkout.ExpiresAt),
        };

        var query = Canonical(parameters);
        return $"{_options.PayUrl}?{query}&vnp_SecureHash={Sign(query, _options.HashSecret)}";
    }

    public VnPayNotification? Read(IReadOnlyDictionary<string, string> query)
    {
        if (string.IsNullOrWhiteSpace(_options.HashSecret) || string.IsNullOrWhiteSpace(_options.TmnCode))
        {
            return null;
        }

        if (!query.TryGetValue("vnp_SecureHash", out var received) || string.IsNullOrEmpty(received))
        {
            return null;
        }

        var expected = Sign(Canonical(query), _options.HashSecret);

        // A fixed-time comparison: a public endpoint must not tell a forger how many characters were right.
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(received.ToLowerInvariant())))
        {
            return null;
        }

        // Signed, but for another merchant: not ours to believe.
        if (Value(query, "vnp_TmnCode") != _options.TmnCode)
        {
            return null;
        }

        if (!long.TryParse(Value(query, "vnp_Amount"), NumberStyles.None, CultureInfo.InvariantCulture, out var hundredths))
        {
            return null;
        }

        var responseCode = Value(query, "vnp_ResponseCode") ?? "";
        var transactionStatus = Value(query, "vnp_TransactionStatus") ?? "";

        return new VnPayNotification(
            Reference: Value(query, "vnp_TxnRef") ?? "",
            Amount: hundredths / 100m,
            ResponseCode: responseCode,
            TransactionStatus: transactionStatus,
            TransactionNo: Value(query, "vnp_TransactionNo"),
            Succeeded: responseCode == "00" && transactionStatus == "00");
    }

    /// <summary>The string VNPay signs: sorted, encoded, joined. Public for the tests that pin it to a fixed vector.</summary>
    public static string Canonical(IEnumerable<KeyValuePair<string, string>> parameters) =>
        string.Join("&", parameters
            .Where(p => p.Key.StartsWith("vnp_", StringComparison.Ordinal)
                        && p.Key is not ("vnp_SecureHash" or "vnp_SecureHashType")
                        && !string.IsNullOrEmpty(p.Value))
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => $"{WebUtility.UrlEncode(p.Key)}={WebUtility.UrlEncode(p.Value)}"));

    public static string Sign(string data, string secret)
    {
        var hash = HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(data));
        return Convert.ToHexStringLower(hash);
    }

    private static string VietnamTime(DateTime utc) =>
        (DateTime.SpecifyKind(utc, DateTimeKind.Utc) + VietnamOffset).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

    private static string? Value(IReadOnlyDictionary<string, string> query, string key) =>
        query.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : null;
}
