using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Ecommerce.Application.Email;
using Microsoft.Extensions.Options;

namespace Ecommerce.Infrastructure.Email;

/// <summary>Where emails go (specs/060): <c>SMTP_HOST</c>/<c>SMTP_PORT</c> override <c>Email:Smtp*</c>.</summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Email";

    /// <summary>Mailpit's SMTP port on this machine by default - a development stack never reaches a real inbox.</summary>
    public string SmtpHost { get; set; } = "localhost";

    public int SmtpPort { get; set; } = 1025;

    public string From { get; set; } = "e-commerce <no-reply@ecommerce.local>";
}

/// <summary>
/// Plain SMTP, no authentication and no TLS - what Mailpit speaks. A real provider replaces this class, the
/// way <c>StubPaymentGateway</c> is the seam a real payment provider replaces.
/// </summary>
public class SmtpEmailTransport(IOptions<SmtpOptions> options) : IEmailTransport
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendAsync(string to, string subject, string text, string html, CancellationToken cancellationToken = default)
    {
        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort) { EnableSsl = false };
        using var message = new MailMessage(new MailAddress(ParseAddress(_options.From).Address, ParseAddress(_options.From).DisplayName), new MailAddress(to))
        {
            Subject = subject,
            SubjectEncoding = Encoding.UTF8,
        };

        // multipart/alternative, text first: a client shows the LAST part it can render, so HTML where it can and
        // the text where it cannot (specs/077).
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(text, Encoding.UTF8, MediaTypeNames.Text.Plain));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(Document(subject, html), Encoding.UTF8, MediaTypeNames.Text.Html));

        await client.SendMailAsync(message, cancellationToken);
    }

    /// <summary>The body in a minimal document with a readable default look - an email's styles have to be inline.</summary>
    private static string Document(string subject, string html) =>
        "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>" + System.Net.WebUtility.HtmlEncode(subject) + "</title></head>"
        + "<body style=\"margin:0;padding:24px;background:#f6f6f6\">"
        + "<div style=\"max-width:560px;margin:0 auto;padding:24px;background:#ffffff;border-radius:12px;"
        + "font-family:Arial,Helvetica,sans-serif;font-size:15px;line-height:1.6;color:#1f2937\">"
        + html
        + "</div></body></html>";

    private static MailAddress ParseAddress(string from) => new(from);
}
