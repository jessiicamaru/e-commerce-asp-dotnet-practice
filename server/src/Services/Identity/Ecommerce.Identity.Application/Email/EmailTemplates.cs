using System.Globalization;
using Ecommerce.Shared.Email;

namespace Ecommerce.Application.Email;

/// <summary>A rendered email: what it says, in the language it was asked for.</summary>
public sealed record RenderedEmail(string Subject, string Body);

/// <summary>
/// The words of every email, in Vietnamese and English (specs/060), filled in when the email is SENT - the
/// request carries a template and data, never a sentence, like a notification (specs/042).
/// </summary>
/// <remarks>
/// Vietnamese is the fallback, as it is for the shop's own text (specs/021). A template this service has no
/// words for, or data missing a value its words need, renders as nothing: an email with a hole in it is worse
/// than one never sent, and the caller marks it <c>Failed</c> with the reason.
/// </remarks>
public static class EmailTemplates
{
    public const string DefaultLanguage = "vi";

    private static readonly Dictionary<(string Template, string Language), (string Subject, string Body)> Words = new()
    {
        [(EmailTemplate.OrderPaid, "vi")] = (
            "Đơn hàng {order} đã được thanh toán",
            "Xin chào {name},\n\n"
            + "Cảm ơn bạn đã mua hàng. Đơn {order} đã được thanh toán: {total}.\n"
            + "Chúng tôi sẽ sớm chuẩn bị hàng và báo cho bạn khi gửi đi.\n\n"
            + "Xem đơn hàng: {link}\n\n"
            + "- e-commerce"),
        [(EmailTemplate.OrderPaid, "en")] = (
            "Your order {order} is paid",
            "Hi {name},\n\n"
            + "Thank you for your order. Order {order} is paid: {total}.\n"
            + "We will prepare it soon and tell you when it ships.\n\n"
            + "See your order: {link}\n\n"
            + "- e-commerce"),
    };

    /// <summary>Every template with words, for a test that holds the list and the constants together.</summary>
    public static IEnumerable<(string Template, string Language)> Known => Words.Keys;

    public static RenderedEmail? Render(
        string template, string language, IReadOnlyDictionary<string, string> data, string recipientName, string storefrontUrl)
    {
        var lang = Words.ContainsKey((template, language)) ? language : DefaultLanguage;
        if (!Words.TryGetValue((template, lang), out var words))
        {
            return null;
        }

        var values = Values(template, lang, data, recipientName, storefrontUrl);
        if (values is null)
        {
            return null;
        }

        return new RenderedEmail(Fill(words.Subject, values), Fill(words.Body, values));
    }

    private static Dictionary<string, string>? Values(
        string template, string language, IReadOnlyDictionary<string, string> data, string name, string storefrontUrl)
    {
        switch (template)
        {
            case EmailTemplate.OrderPaid:
                if (!data.TryGetValue("orderId", out var orderId)
                    || !data.TryGetValue("total", out var total)
                    || !data.TryGetValue("currency", out var currency))
                {
                    return null;
                }

                return new()
                {
                    ["name"] = name,
                    ["order"] = orderId.Length >= 8 ? orderId[..8] : orderId,
                    ["total"] = Money(total, currency, language),
                    ["link"] = $"{storefrontUrl.TrimEnd('/')}/orders/{orderId}",
                };

            default:
                return null;
        }
    }

    /// <summary>The amount in the reader's number format, in its currency's minor unit - dong has none.</summary>
    private static string Money(string amount, string currency, string language)
    {
        if (!decimal.TryParse(amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            return $"{amount} {currency}";
        }

        var culture = CultureInfo.GetCultureInfo(language == "vi" ? "vi-VN" : "en-US");
        var decimals = currency.Equals("VND", StringComparison.OrdinalIgnoreCase) ? 0 : 2;
        return $"{value.ToString("N" + decimals, culture)} {currency}";
    }

    private static string Fill(string text, Dictionary<string, string> values) =>
        values.Aggregate(text, (current, pair) => current.Replace("{" + pair.Key + "}", pair.Value));
}
