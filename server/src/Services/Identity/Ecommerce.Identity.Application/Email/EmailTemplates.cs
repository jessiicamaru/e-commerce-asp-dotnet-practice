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

        // specs/083: what reached a person only as a notice in the bell until then.
        [(EmailTemplate.ParcelShipped, "vi")] = (
            "Đơn hàng {order} đang trên đường đến bạn",
            "Xin chào {name},\n\n"
            + "Một kiện hàng của đơn {order} từ {shop} đã được gửi đi.\n"
            + "Mã vận đơn: {tracking}\n\n"
            + "Theo dõi đơn hàng: {link}\n\n"
            + "- e-commerce"),
        [(EmailTemplate.ParcelShipped, "en")] = (
            "Your order {order} is on its way",
            "Hi {name},\n\n"
            + "A parcel of order {order} from {shop} has shipped.\n"
            + "Tracking reference: {tracking}\n\n"
            + "Follow your order: {link}\n\n"
            + "- e-commerce"),
        [(EmailTemplate.OrderCancelled, "vi")] = (
            "Đơn hàng {order} đã được hủy",
            "Xin chào {name},\n\n"
            + "Đơn {order} đã được hủy. Toàn bộ số tiền {total} sẽ được hoàn lại cho bạn.\n\n"
            + "Xem đơn hàng: {link}\n\n"
            + "- e-commerce"),
        [(EmailTemplate.OrderCancelled, "en")] = (
            "Your order {order} is cancelled",
            "Hi {name},\n\n"
            + "Order {order} has been cancelled. The whole {total} will be refunded to you.\n\n"
            + "See your order: {link}\n\n"
            + "- e-commerce"),
        [(EmailTemplate.ReturnAccepted, "vi")] = (
            "Yêu cầu trả hàng cho đơn {order} đã được chấp nhận",
            "Xin chào {name},\n\n"
            + "Yêu cầu trả hàng của bạn cho đơn {order} đã được chấp nhận.\n"
            + "Hãy gửi kiện hàng lại và nhập mã vận đơn trên trang đơn hàng.\n\n"
            + "{link}\n\n"
            + "- e-commerce"),
        [(EmailTemplate.ReturnAccepted, "en")] = (
            "Your return for order {order} is accepted",
            "Hi {name},\n\n"
            + "Your return for order {order} has been accepted.\n"
            + "Send the parcel back and enter its tracking reference on the order's page.\n\n"
            + "{link}\n\n"
            + "- e-commerce"),
        [(EmailTemplate.ReturnRefused, "vi")] = (
            "Yêu cầu trả hàng cho đơn {order} bị từ chối",
            "Xin chào {name},\n\n"
            + "Yêu cầu trả hàng của bạn cho đơn {order} đã bị từ chối.\n"
            + "Lý do: {reason}\n\n"
            + "Xem đơn hàng: {link}\n\n"
            + "- e-commerce"),
        [(EmailTemplate.ReturnRefused, "en")] = (
            "Your return for order {order} was refused",
            "Hi {name},\n\n"
            + "Your return for order {order} was refused.\n"
            + "Reason: {reason}\n\n"
            + "See your order: {link}\n\n"
            + "- e-commerce"),
        [(EmailTemplate.ReturnRefunded, "vi")] = (
            "Đã nhận hàng trả của đơn {order}",
            "Xin chào {name},\n\n"
            + "Kiện hàng bạn trả lại của đơn {order} đã về đến nơi. Số tiền {amount} sẽ được hoàn lại cho bạn.\n\n"
            + "Xem đơn hàng: {link}\n\n"
            + "- e-commerce"),
        [(EmailTemplate.ReturnRefunded, "en")] = (
            "Your return for order {order} arrived",
            "Hi {name},\n\n"
            + "The parcel you returned from order {order} has arrived. {amount} will be refunded to you.\n\n"
            + "See your order: {link}\n\n"
            + "- e-commerce"),
        [(EmailTemplate.SavedBackInStock, "vi")] = (
            "{product} đã có hàng trở lại",
            "Xin chào {name},\n\n"
            + "{product}, sản phẩm bạn đã lưu, đã có hàng trở lại.\n\n"
            + "Xem sản phẩm: {link}\n\n"
            + "- e-commerce"),
        [(EmailTemplate.SavedBackInStock, "en")] = (
            "{product} is back in stock",
            "Hi {name},\n\n"
            + "{product}, which you saved, is back in stock.\n\n"
            + "See it: {link}\n\n"
            + "- e-commerce"),
        [(EmailTemplate.AccountLocked, "vi")] = (
            "Tài khoản của bạn đã bị khóa",
            "Xin chào {name},\n\n"
            + "Tài khoản của bạn đã bị khóa đến {until}.\n"
            + "Lý do: {reason}\n\n"
            + "Sau thời điểm đó bạn có thể đăng nhập lại như bình thường.\n\n"
            + "- e-commerce"),
        [(EmailTemplate.AccountLocked, "en")] = (
            "Your account is locked",
            "Hi {name},\n\n"
            + "Your account has been locked until {until}.\n"
            + "Reason: {reason}\n\n"
            + "After that you can sign in again as usual.\n\n"
            + "- e-commerce"),
        [(EmailTemplate.AccountBanned, "vi")] = (
            "Tài khoản của bạn đã bị cấm",
            "Xin chào {name},\n\n"
            + "Tài khoản của bạn đã bị cấm và không thể đăng nhập nữa.\n"
            + "Lý do: {reason}\n\n"
            + "- e-commerce"),
        [(EmailTemplate.AccountBanned, "en")] = (
            "Your account is banned",
            "Hi {name},\n\n"
            + "Your account has been banned and can no longer sign in.\n"
            + "Reason: {reason}\n\n"
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
    public static readonly IReadOnlyList<string> Templates =
    [
        EmailTemplate.OrderPaid, EmailTemplate.ParcelShipped, EmailTemplate.OrderCancelled,
        EmailTemplate.ReturnAccepted, EmailTemplate.ReturnRefused, EmailTemplate.ReturnRefunded,
        EmailTemplate.SavedBackInStock,
        EmailTemplate.PasswordReset, EmailTemplate.EmailConfirmation, EmailTemplate.AccountLocked, EmailTemplate.AccountBanned,
    ];

    /// <summary>The languages an email is written in - the shop's (specs/021).</summary>
    public static readonly IReadOnlyList<string> Languages = ["vi", "en"];

    /// <summary>
    /// <paramref name="language"/> when an email can be written in it ("en-US" counts as "en"), else null - so a
    /// header asking for Klingon records nothing rather than a language no email is written in (specs/083).
    /// </summary>
    public static string? Supported(string? language)
    {
        var tag = language?.Trim().ToLowerInvariant() ?? string.Empty;
        tag = tag.Length > 2 ? tag[..2] : tag;
        return Languages.Contains(tag) ? tag : null;
    }

    /// <summary>What each template's words may use - what <see cref="Values"/> fills in. Anything else is refused on save.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> PlaceholdersOf = new Dictionary<string, IReadOnlyList<string>>
    {
        [EmailTemplate.OrderPaid] = ["name", "order", "total", "link"],
        [EmailTemplate.PasswordReset] = ["name", "link"],
        [EmailTemplate.EmailConfirmation] = ["name", "link"],
        [EmailTemplate.ParcelShipped] = ["name", "order", "shop", "tracking", "link"],
        [EmailTemplate.OrderCancelled] = ["name", "order", "total", "link"],
        [EmailTemplate.ReturnAccepted] = ["name", "order", "link"],
        [EmailTemplate.ReturnRefused] = ["name", "order", "reason", "link"],
        [EmailTemplate.ReturnRefunded] = ["name", "order", "amount", "link"],
        [EmailTemplate.SavedBackInStock] = ["name", "product", "link"],
        [EmailTemplate.AccountLocked] = ["name", "until", "reason"],
        [EmailTemplate.AccountBanned] = ["name", "reason"],
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
        [EmailTemplate.ParcelShipped] = [],
        [EmailTemplate.OrderCancelled] = [],
        [EmailTemplate.ReturnAccepted] = [],
        [EmailTemplate.ReturnRefused] = [],
        [EmailTemplate.ReturnRefunded] = [],
        [EmailTemplate.SavedBackInStock] = [],
        [EmailTemplate.AccountLocked] = [],
        [EmailTemplate.AccountBanned] = [],
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
    private const string SampleOrder = "01a0dd2b-sample";

    public static IReadOnlyDictionary<string, string> SampleData(string template) => template switch
    {
        EmailTemplate.OrderPaid or EmailTemplate.OrderCancelled =>
            new Dictionary<string, string> { ["orderId"] = SampleOrder, ["total"] = "1250000", ["currency"] = "VND" },
        EmailTemplate.ParcelShipped =>
            new Dictionary<string, string> { ["orderId"] = SampleOrder, ["tracking"] = "VNPOST-123456", ["shop"] = "Leica Corner" },
        EmailTemplate.ReturnAccepted => new Dictionary<string, string> { ["orderId"] = SampleOrder },
        EmailTemplate.ReturnRefused => new Dictionary<string, string> { ["orderId"] = SampleOrder, ["reason"] = "The seal is broken." },
        EmailTemplate.ReturnRefunded => new Dictionary<string, string> { ["orderId"] = SampleOrder, ["amount"] = "990000", ["currency"] = "VND" },
        EmailTemplate.SavedBackInStock => new Dictionary<string, string> { ["productId"] = "01a0dd2b-0000-7000-8000-000000000000", ["product"] = "Fujifilm X-T5" },
        EmailTemplate.AccountLocked => new Dictionary<string, string> { ["until"] = "2031-01-15T03:30:00.0000000Z", ["reason"] = "Repeated spam in questions." },
        EmailTemplate.AccountBanned => new Dictionary<string, string> { ["reason"] = "Fraud." },
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

            case EmailTemplate.ParcelShipped when data.TryGetValue("orderId", out var shipped):
                return new()
                {
                    ["name"] = name,
                    ["order"] = Short(shipped),
                    // The shop's own parcel names no seller; it is the shop's.
                    ["shop"] = data.TryGetValue("shop", out var shop) && !string.IsNullOrWhiteSpace(shop) ? shop : "e-commerce",
                    ["tracking"] = data.TryGetValue("tracking", out var tracking) && !string.IsNullOrWhiteSpace(tracking) ? tracking : "-",
                    ["link"] = OrderLink(storefrontUrl, shipped),
                };

            case EmailTemplate.OrderCancelled
                when data.TryGetValue("orderId", out var cancelled)
                     && data.TryGetValue("total", out var refund)
                     && data.TryGetValue("currency", out var refundCurrency):
                return new()
                {
                    ["name"] = name,
                    ["order"] = Short(cancelled),
                    ["total"] = Money(refund, refundCurrency, language),
                    ["link"] = OrderLink(storefrontUrl, cancelled),
                };

            case EmailTemplate.ReturnAccepted when data.TryGetValue("orderId", out var accepted):
                return new() { ["name"] = name, ["order"] = Short(accepted), ["link"] = OrderLink(storefrontUrl, accepted) };

            case EmailTemplate.ReturnRefused when data.TryGetValue("orderId", out var refused):
                return new()
                {
                    ["name"] = name,
                    ["order"] = Short(refused),
                    ["reason"] = data.GetValueOrDefault("reason") ?? string.Empty,
                    ["link"] = OrderLink(storefrontUrl, refused),
                };

            case EmailTemplate.ReturnRefunded
                when data.TryGetValue("orderId", out var returned)
                     && data.TryGetValue("amount", out var amount)
                     && data.TryGetValue("currency", out var amountCurrency):
                return new()
                {
                    ["name"] = name,
                    ["order"] = Short(returned),
                    ["amount"] = Money(amount, amountCurrency, language),
                    ["link"] = OrderLink(storefrontUrl, returned),
                };

            case EmailTemplate.SavedBackInStock
                when data.TryGetValue("productId", out var productId) && data.TryGetValue("product", out var product):
                return new()
                {
                    ["name"] = name,
                    ["product"] = product,
                    ["link"] = $"{storefrontUrl.TrimEnd('/')}/products/{Uri.EscapeDataString(productId)}",
                };

            case EmailTemplate.AccountLocked
                when data.TryGetValue("until", out var until)
                     && DateTime.TryParse(until, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var lockedUntil):
                return new()
                {
                    ["name"] = name,
                    // In UTC and saying so: an email has no reader's time zone to write in.
                    ["until"] = lockedUntil.ToString(language == "vi" ? "HH:mm dd/MM/yyyy" : "MMM d, yyyy HH:mm", CultureInfo.GetCultureInfo(language == "vi" ? "vi-VN" : "en-US")) + " UTC",
                    ["reason"] = data.GetValueOrDefault("reason") ?? string.Empty,
                };

            case EmailTemplate.AccountBanned:
                return new() { ["name"] = name, ["reason"] = data.GetValueOrDefault("reason") ?? string.Empty };

            default:
                return null;
        }
    }

    /// <summary>An order as a person reads it: the first eight characters of its id, as the storefront shows it.</summary>
    private static string Short(string orderId) => orderId.Length >= 8 ? orderId[..8] : orderId;

    private static string OrderLink(string storefrontUrl, string orderId) =>
        $"{storefrontUrl.TrimEnd('/')}/orders/{Uri.EscapeDataString(orderId)}";

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
