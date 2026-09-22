using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Ecommerce.Shared.Localization;

/// <summary>
/// Which language this request wants to be answered in (specs/021).
/// </summary>
/// <remarks>
/// The Application layer asks this instead of reaching for <c>HttpContext</c> or for ambient
/// <c>CultureInfo</c>, exactly as it asks <c>ICurrentUser</c> who the caller is. That is what lets a
/// handler be tested by handing it a language rather than a request.
/// </remarks>
public interface IRequestLanguage
{
    /// <summary>A supported tag, always - never empty, and never one the shop does not speak.</summary>
    string Current { get; }
}

public class LanguageOptions
{
    public const string SectionName = "Localization";

    /// <summary>What a request that says nothing gets, and what untranslated text is assumed to be.</summary>
    public string DefaultLanguage { get; init; } = "vi";

    /// <summary>BCP 47 tags. A third language is a row here and a translation file - never a migration.</summary>
    public string[] Supported { get; init; } = ["vi", "en"];
}

/// <summary>
/// The language ASP.NET Core's <c>RequestLocalizationMiddleware</c> negotiated, reduced to a tag this
/// shop speaks.
/// </summary>
/// <remarks>
/// <para>
/// The negotiation itself - <c>?lang=</c>, then <c>Accept-Language</c> with its quality values, then
/// the default - is the framework's, configured in <c>AddRequestLanguage</c>. This only reads the
/// answer, so there is one implementation of the rules rather than two that can disagree.
/// </para>
/// <para>
/// <b>Never from the delivery address.</b> Where a parcel goes says nothing about what its buyer
/// reads, and it would change the language in the middle of a checkout.
/// </para>
/// </remarks>
public class RequestLanguage(IHttpContextAccessor accessor, IOptions<LanguageOptions> options) : IRequestLanguage
{
    private readonly IHttpContextAccessor _accessor = accessor;
    private readonly LanguageOptions _options = options.Value;

    public string Current
    {
        get
        {
            // No request at all - a consumer, a hosted service, a test. The default is the honest
            // answer, and the only one available.
            if (_accessor.HttpContext is null)
            {
                return _options.DefaultLanguage;
            }

            var negotiated = CultureInfo.CurrentUICulture;

            return Supported(negotiated.Name)
                ?? Supported(negotiated.TwoLetterISOLanguageName)
                ?? _options.DefaultLanguage;
        }
    }

    /// <summary>The supported tag this value means, or <c>null</c> if the shop does not speak it.</summary>
    private string? Supported(string? tag) =>
        string.IsNullOrWhiteSpace(tag)
            ? null
            : _options.Supported.FirstOrDefault(supported => string.Equals(supported, tag, StringComparison.OrdinalIgnoreCase));
}

/// <summary>A fixed language, for a consumer or a test that has no request.</summary>
public class FixedLanguage(string language) : IRequestLanguage
{
    public string Current { get; } = language;
}
