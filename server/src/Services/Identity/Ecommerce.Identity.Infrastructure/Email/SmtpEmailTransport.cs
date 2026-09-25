using System.Net.Mail;
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

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort) { EnableSsl = false };
        using var message = new MailMessage(new MailAddress(ParseAddress(_options.From).Address, ParseAddress(_options.From).DisplayName), new MailAddress(to))
        {
            Subject = subject,
            Body = body,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false,
        };

        await client.SendMailAsync(message, cancellationToken);
    }

    private static MailAddress ParseAddress(string from) => new(from);
}
