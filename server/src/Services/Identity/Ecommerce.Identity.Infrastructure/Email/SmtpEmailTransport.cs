using System.Net;
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

    /// <summary>
    /// A real mail server's account (specs/141): <c>SMTP_USERNAME</c> and <c>SMTP_PASSWORD</c>, both or neither. Mailpit in
    /// development needs neither.
    /// </summary>
    public string? Username { get; set; }

    public string? Password { get; set; }

    /// <summary>STARTTLS (<c>SMTP_TLS=true</c>) - what every hosted mail service asks for. Off for Mailpit.</summary>
    public bool UseTls { get; set; }

    /// <summary>What would make sending fail later, said at startup instead.</summary>
    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(Username) != string.IsNullOrWhiteSpace(Password))
            problems.Add("SMTP_USERNAME and SMTP_PASSWORD go together: set both, or neither.");
        if (string.IsNullOrWhiteSpace(SmtpHost))
            problems.Add("SMTP_HOST is empty.");
        if (SmtpPort is < 1 or > 65535)
            problems.Add($"SMTP_PORT {SmtpPort} is not a port.");
        if (!MailAddress.TryCreate(From, out _))
            problems.Add($"SMTP_FROM '{From}' is not an address.");
        return problems;
    }
}

/// <summary>
/// SMTP: plain and anonymous to Mailpit in development, signed in over STARTTLS to a real provider in production
/// (specs/141) - which of the two is configuration, not code.
/// </summary>
public class SmtpEmailTransport(IOptions<SmtpOptions> options) : IEmailTransport
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendAsync(string to, string subject, string text, string html, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(_options);
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

    /// <summary>The connection the options describe: STARTTLS and an account only when configured (specs/141).</summary>
    public static SmtpClient CreateClient(SmtpOptions options)
    {
        var client = new SmtpClient(options.SmtpHost, options.SmtpPort) { EnableSsl = options.UseTls };
        if (!string.IsNullOrWhiteSpace(options.Username))
        {
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(options.Username, options.Password);
        }

        return client;
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

/// <summary>The environment's say over the SMTP options (specs/060, specs/141): each variable, when set, wins.</summary>
public static class SmtpSettings
{
    public static void Apply(SmtpOptions options, Func<string, string?> environment)
    {
        options.SmtpHost = environment("SMTP_HOST") ?? options.SmtpHost;
        if (int.TryParse(environment("SMTP_PORT"), out var port)) options.SmtpPort = port;
        options.Username = environment("SMTP_USERNAME") ?? options.Username;
        options.Password = environment("SMTP_PASSWORD") ?? options.Password;
        if (bool.TryParse(environment("SMTP_TLS"), out var tls)) options.UseTls = tls;
        options.From = environment("SMTP_FROM") is { Length: > 0 } from ? from : options.From;
    }
}
