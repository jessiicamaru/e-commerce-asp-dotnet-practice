using System.Globalization;
using Ecommerce.Shared.Email;

namespace Ecommerce.Application.Email;

/// <summary>
/// A rendered email: what it says, in the language it was asked for - as HTML, and as the plain text every HTML
/// email carries beside it (specs/077).
/// </summary>
public sealed record RenderedEmail(string Subject, string Html, string Text);

/// <summary>An email's words before they are filled in: a subject and an HTML body with <c>{name}</c> placeholders.</summary>
public sealed record EmailWords(string Subject, string BodyHtml);

/// <summary>
/// The words of every email, in Vietnamese and English (specs/060), filled in when the email is SENT - the
/// request carries a template and data, never a sentence, like a notification (specs/042).
/// </summary>
/// <remarks>
/// Vietnamese is the fallback, as it is for the shop's own text (specs/021). A template this service has no
/// words for, or data missing a value its words need, renders as nothing: an email with a hole in it is worse
/// than one never sent, and the caller marks it <c>Failed</c> with the reason.
/// <para>
/// Since specs/077 an administrator can replace the words of any template and language; these are then the
/// DEFAULTS - what an unedited template says, and what "reset to default" goes back to.
/// </para>
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
        [(EmailTemplate.PasswordReset, "vi")] = (
            "Đặt lại mật khẩu của bạn",
            "Xin chào {name},\n\n"
            + "Có người (hy vọng là bạn) đã yêu cầu đặt lại mật khẩu cho tài khoản này.\n"
            + "Mở liên kết dưới đây trong 30 phút để chọn mật khẩu mới. Liên kết chỉ dùng được một lần.\n\n"
            + "{link}\n\n"
            + "Nếu không phải bạn, hãy bỏ qua email này - mật khẩu của bạn không đổi.\n\n"
            + "- e-commerce"),
        [(EmailTemplate.PasswordReset, "en")] = (
            "Reset your password",
            "Hi {name},\n\n"
            + "Somebody (hopefully you) asked to reset the password of this account.\n"
            + "Open the link below within 30 minutes to choose a new one. It works once.\n\n"
            + "{link}\n\n"
            + "If it was not you, ignore this email - your password has not changed.\n\n"
            + "- e-commerce"),
        [(EmailTemplate.EmailConfirmation, "vi")] = (
            "Xác nhận địa chỉ email của bạn",
            "Xin chào {name},\n\n"
            + "Cảm ơn bạn đã đăng ký. Mở liên kết dưới đây trong 24 giờ để xác nhận email này là của bạn.\n\n"
            + "{link}\n\n"
            + "Nếu bạn không đăng ký, hãy bỏ qua email này.\n\n"
            + "- e-commerce"),
        [(EmailTemplate.EmailConfirmation, "en")] = (
            "Confirm your email address",
            "Hi {name},\n\n"
            + "Thanks for signing up. Open the link below within 24 hours to confirm this address is yours.\n\n"
            + "{link}\n\n"
            + "If you did not sign up, ignore this email.\n\n"
            + "- e-commerce"),
    };

    /// <summary>
    /// Templates whose data is a secret (specs/061): once sent, the row keeps no copy of it. Delivery needs it;
    /// nothing afterwards does.
    /// </summary>
    public static readonly IReadOnlySet<string> ScrubbedOnceSent = new HashSet<string> { EmailTemplate.PasswordReset, EmailTemplate.EmailConfirmation };

    /// <summary>Every template with words, for a test that holds the list and the constants together.</summary>
    public static IEnumerable<(string Template, string Language)> Known => Words.Keys;

    /// <summary>The templates an administrator can edit, in the order the console lists them.</summary>
    public static readonly IReadOnlyList<string> Templates = [EmailTemplate.OrderPaid, EmailTemplate.PasswordReset, EmailTemplate.EmailConfirmation];

    /// <summary>The languages an email is written in - the shop's (specs/021).</summary>
    public static readonly IReadOnlyList<string> Languages = ["vi", "en"];

    /// <summary>What each template's words may use - what <see cref="Values"/> fills in. Anything else is refused on save.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> PlaceholdersOf = new Dictionary<string, IReadOnlyList<string>>
    {
        [EmailTemplate.OrderPaid] = ["name", "order", "total", "link"],
        [EmailTemplate.PasswordReset] = ["name", "link"],
        [EmailTemplate.EmailConfirmation] = ["name", "link"],
    };

    /// <summary>
    /// What an edit may not remove (specs/077): a reset or confirmation email without its link is an email that
    /// cannot do the one thing it is for.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> RequiredOf = new Dictionary<string, IReadOnlyList<string>>
    {
        [EmailTemplate.OrderPaid] = [],
        [EmailTemplate.PasswordReset] = ["link"],
        [EmailTemplate.EmailConfirmation] = ["link"],
    };

    /// <summary>The built-in words of a template in a language, as HTML - null when there are none.</summary>
    public static EmailWords? Default(string template, string language) =>
        Words.TryGetValue((template, language), out var words) ? new EmailWords(words.Subject, EmailHtml.FromPlainText(words.Body)) : null;

    /// <summary>
    /// Fills a template's words for one recipient. <paramref name="edited"/> is an administrator's version (already
    /// sanitised); without one, the built-in words. Null when there are no words or the data lacks a value.
    /// </summary>
    public static RenderedEmail? Render(
        string template, string language, IReadOnlyDictionary<string, string> data, string recipientName, string storefrontUrl,
        EmailWords? edited = null)
    {
        var lang = Words.ContainsKey((template, language)) ? language : DefaultLanguage;
        var words = edited ?? Default(template, lang);
        if (words is null)
        {
            return null;
        }

        var values = Values(template, lang, data, recipientName, storefrontUrl);
        if (values is null)
        {
            return null;
        }

        var html = EmailHtml.FillHtml(words.BodyHtml, values);
        return new RenderedEmail(EmailHtml.FillSubject(words.Subject, values), html, EmailHtml.ToText(html));
    }

    /// <summary>
    /// Made-up data a template renders with in the console's preview and test email (specs/077) - never a real
    /// order and never a real token.
    /// </summary>
    public static IReadOnlyDictionary<string, string> SampleData(string template) => template switch
    {
        EmailTemplate.OrderPaid => new Dictionary<string, string> { ["orderId"] = "01a0dd2b-sample", ["total"] = "1250000", ["currency"] = "VND" },
        _ => new Dictionary<string, string> { ["token"] = "sample-token" },
    };

    public static Dictionary<string, string>? Values(
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

            case EmailTemplate.PasswordReset:
                if (!data.TryGetValue("token", out var token) || string.IsNullOrWhiteSpace(token))
                {
                    return null;
                }

                return new()
                {
                    ["name"] = name,
                    ["link"] = $"{storefrontUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(token)}",
                };

            case EmailTemplate.EmailConfirmation:
                if (!data.TryGetValue("token", out var confirmation) || string.IsNullOrWhiteSpace(confirmation))
                {
                    return null;
                }

                return new()
                {
                    ["name"] = name,
                    ["link"] = $"{storefrontUrl.TrimEnd('/')}/confirm-email?token={Uri.EscapeDataString(confirmation)}",
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

}
