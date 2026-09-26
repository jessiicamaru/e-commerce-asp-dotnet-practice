using HtmlSanitizer = Ganss.Xss.HtmlSanitizer;

namespace Ecommerce.Infrastructure.Email;

/// <summary>
/// What an email body may contain (specs/077): text formatting, headings, lists, quotes, rules and links - to http,
/// https or mailto only. No scripts, no event handlers, no styles, no images, no frames, no forms. An administrator
/// is trusted; what they paste from elsewhere is not, and it lands in people's inboxes.
/// </summary>
/// <remarks>
/// A placeholder in a link - <c>href="{link}"</c> - survives: it is a relative address to the sanitiser, and the
/// value filled in later is the storefront's own URL, HTML-escaped.
/// </remarks>
public sealed class AllowListHtmlSanitizer : Ecommerce.Application.Email.IHtmlSanitizer
{
    public static readonly IReadOnlyList<string> Tags =
        ["p", "br", "strong", "b", "em", "i", "u", "s", "a", "h1", "h2", "h3", "ul", "ol", "li", "blockquote", "hr"];

    private readonly HtmlSanitizer _sanitizer;

    public AllowListHtmlSanitizer()
    {
        _sanitizer = new HtmlSanitizer();
        _sanitizer.AllowedTags.Clear();
        foreach (var tag in Tags)
            _sanitizer.AllowedTags.Add(tag);
        _sanitizer.AllowedAttributes.Clear();
        _sanitizer.AllowedAttributes.Add("href");
        _sanitizer.AllowedSchemes.Clear();
        foreach (var scheme in new[] { "http", "https", "mailto" })
            _sanitizer.AllowedSchemes.Add(scheme);
        _sanitizer.AllowedCssProperties.Clear();
        _sanitizer.AllowedAtRules.Clear();
        _sanitizer.AllowedClasses.Clear();
        _sanitizer.UriAttributes.Clear();
        _sanitizer.UriAttributes.Add("href");
    }

    public string Sanitize(string html) => _sanitizer.Sanitize(html);
}
