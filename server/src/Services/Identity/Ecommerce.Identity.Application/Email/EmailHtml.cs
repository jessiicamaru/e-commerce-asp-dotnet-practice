using System.Net;
using System.Text.RegularExpressions;

namespace Ecommerce.Application.Email;

/// <summary>
/// An email's words as HTML (specs/077): the built-in plain text turned into HTML, the placeholders a template uses,
/// the values filled in - escaped, since a name is whatever somebody typed - and the plain-text alternative every
/// HTML email carries.
/// </summary>
public static partial class EmailHtml
{
    [GeneratedRegex(@"\{([A-Za-z]+)\}")]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex(@"<a\b[^>]*\bhref=""([^""]*)""[^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex LinkPattern();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BreakPattern();

    [GeneratedRegex(@"</(p|h[1-6]|blockquote|ul|ol)>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockEndPattern();

    [GeneratedRegex(@"<li\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItemPattern();

    [GeneratedRegex(@"<hr\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex RulePattern();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"[ \t]*\n[ \t]*")]
    private static partial Regex LineSpacePattern();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLinesPattern();

    /// <summary>The names a text uses as <c>{name}</c>, each once, in the order they first appear.</summary>
    public static IReadOnlyList<string> Placeholders(string text) =>
        PlaceholderPattern().Matches(text).Select(m => m.Groups[1].Value).Distinct().ToList();

    /// <summary>
    /// The built-in plain text as HTML: a paragraph per blank-line block, a line break per line, and <c>{link}</c> as a
    /// link to itself - so an unedited template reads as it always did.
    /// </summary>
    public static string FromPlainText(string text) =>
        string.Concat(text.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries).Select(block =>
            "<p>" + Encode(block).Replace("\n", "<br>").Replace("{link}", "<a href=\"{link}\">{link}</a>") + "</p>"));

    /// <summary>Fills <c>{name}</c> placeholders in HTML, each value HTML-escaped - in text and in an href alike.</summary>
    public static string FillHtml(string html, IReadOnlyDictionary<string, string> values) =>
        values.Aggregate(html, (current, pair) => current.Replace("{" + pair.Key + "}", Encode(pair.Value)));

    /// <summary>
    /// Escapes the five characters HTML gives meaning to, and nothing else: <c>WebUtility.HtmlEncode</c> also turns
    /// letters like "é" into numeric entities, which then show up in the editor.
    /// </summary>
    public static string Encode(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");

    /// <summary>Fills a subject: one line, no markup - it is a header, not HTML.</summary>
    public static string FillSubject(string subject, IReadOnlyDictionary<string, string> values) =>
        values.Aggregate(subject, (current, pair) => current.Replace("{" + pair.Key + "}", pair.Value))
            .Replace("\r", " ").Replace("\n", " ").Trim();

    /// <summary>
    /// The plain-text alternative: paragraphs and line breaks kept, a link as its address (or "words (address)" when
    /// its words say something else), lists as dashes, everything else as its text.
    /// </summary>
    public static string ToText(string html)
    {
        var text = LinkPattern().Replace(html, m =>
        {
            var href = WebUtility.HtmlDecode(m.Groups[1].Value);
            var words = WebUtility.HtmlDecode(TagPattern().Replace(m.Groups[2].Value, "")).Trim();
            return words.Length == 0 || words == href ? href : $"{words} ({href})";
        });
        text = BreakPattern().Replace(text, "\n");
        text = ListItemPattern().Replace(text, "\n- ");
        text = RulePattern().Replace(text, "\n----\n");
        text = BlockEndPattern().Replace(text, "\n\n");
        text = WebUtility.HtmlDecode(TagPattern().Replace(text, ""));
        text = LineSpacePattern().Replace(text, "\n");
        return BlankLinesPattern().Replace(text, "\n\n").Trim();
    }
}
