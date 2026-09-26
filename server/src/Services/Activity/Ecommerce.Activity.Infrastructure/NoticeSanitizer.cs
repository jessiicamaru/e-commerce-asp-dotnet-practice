using Ecommerce.Activity.Application.Common.Interfaces;
using HtmlSanitizer = Ganss.Xss.HtmlSanitizer;

namespace Ecommerce.Activity.Infrastructure;

/// <summary>
/// What a notice's words may contain (specs/078): bold, italic, underline and links - one line in a bell, never a
/// layout. A link's address is checked beside this (the web or a page of the shop); here only its scheme.
/// </summary>
public sealed class NoticeSanitizer : INoticeSanitizer
{
    public static readonly IReadOnlyList<string> Tags = ["strong", "b", "em", "i", "u", "a"];

    private readonly HtmlSanitizer _sanitizer;

    public NoticeSanitizer()
    {
        _sanitizer = new HtmlSanitizer { KeepChildNodes = true };
        _sanitizer.AllowedTags.Clear();
        foreach (var tag in Tags)
            _sanitizer.AllowedTags.Add(tag);
        _sanitizer.AllowedAttributes.Clear();
        _sanitizer.AllowedAttributes.Add("href");
        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.Add("http");
        _sanitizer.AllowedSchemes.Add("https");
        _sanitizer.AllowedCssProperties.Clear();
        _sanitizer.AllowedAtRules.Clear();
        _sanitizer.AllowedClasses.Clear();
        _sanitizer.UriAttributes.Clear();
        _sanitizer.UriAttributes.Add("href");

        // A paragraph an editor wraps the line in is dropped for its words (KeepChildNodes); a script's or a
        // style's words are not words anybody wrote, so they go with it.
        _sanitizer.RemovingTag += (_, e) =>
        {
            if (e.Tag.NodeName is "SCRIPT" or "STYLE" or "IFRAME" or "OBJECT" or "TEMPLATE" or "NOSCRIPT")
                e.Tag.TextContent = string.Empty;
        };
    }

    public string Sanitize(string html) => _sanitizer.Sanitize(html);
}
