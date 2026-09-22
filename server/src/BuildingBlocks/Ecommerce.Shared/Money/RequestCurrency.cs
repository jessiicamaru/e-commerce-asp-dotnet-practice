using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Ecommerce.Shared.Money;

/// <summary>
/// Which currency this request wants its amounts in (specs/022).
/// </summary>
/// <remarks>
/// The Application layer asks this instead of reaching for <c>HttpContext</c>, exactly as it asks
/// <c>ICurrentUser</c> who the caller is and <c>IRequestLanguage</c> what they read. That is what lets
/// a handler be tested by handing it a currency rather than a request.
/// </remarks>
public interface IRequestCurrency
{
    /// <summary>A supported currency, always - never null, and never one the shop does not price in.</summary>
    Currency Current { get; }
}

public class CurrencyOptions
{
    public const string SectionName = "Money";

    /// <summary>What a request that says nothing gets, and what an amount with no currency means.</summary>
    public string DefaultCurrency { get; init; } = "VND";

    /// <summary>
    /// A third currency is a row here and a price list - never a migration.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>No default value, deliberately.</b> The configuration binder <i>appends</i> to an array
    /// that already has contents rather than replacing it, so an initializer of
    /// <c>[VND, USD]</c> plus a configured <c>[VND, USD]</c> yields <c>[VND, USD, VND, USD]</c>. The
    /// duplicate check in <c>AddRequestCurrency</c> is what found this. A service that wants
    /// currencies configures them; one that does not, does not start.
    /// </remarks>
    public CurrencySetting[] Supported { get; init; } = [];

    public class CurrencySetting
    {
        public string Code { get; init; } = string.Empty;

        public int Decimals { get; init; }
    }
}

/// <summary>
/// The currency asked for by <c>?currency=</c>, then <c>X-Currency</c>, then the configured default.
/// </summary>
/// <remarks>
/// <para>
/// <b>Hand-rolled on purpose, unlike the language.</b> Language negotiation is ASP.NET Core's because
/// <c>Accept-Language</c> is a standard header with quality values and region fallback that are easy
/// to get wrong. There is no standard request header for a currency - a currency is a property of the
/// offer, not of the representation - so this is an exact match on a code, which has no subtleties to
/// get wrong.
/// </para>
/// <para>
/// <b>Never from the language.</b> Most of this shop's customers are Vietnamese people reading
/// English, and they pay in dong. Language is what you read; currency is what you pay (research D1).
/// </para>
/// <para>
/// <b>Never from the delivery address.</b> It is chosen in the middle of checkout, after the shopper
/// has seen every price.
/// </para>
/// </remarks>
public class RequestCurrency(IHttpContextAccessor accessor, IOptions<CurrencyOptions> options) : IRequestCurrency
{
    /// <summary>The header the storefront sends. Not standard, and not pretending to be.</summary>
    public const string HeaderName = "X-Currency";

    public const string QueryKey = "currency";

    private readonly IHttpContextAccessor _accessor = accessor;
    private readonly CurrencyOptions _options = options.Value;

    public Currency Current
    {
        get
        {
            var context = _accessor.HttpContext;

            // No request at all - a consumer, a hosted service, a test. The default is the honest
            // answer, and the only one available.
            if (context is null)
            {
                return Default;
            }

            // ?currency= first, so a link carries the currency its prices were quoted in.
            return Supported(context.Request.Query[QueryKey])
                ?? Supported(context.Request.Headers[HeaderName])
                ?? Default;
        }
    }

    private Currency Default => Resolve(_options, _options.DefaultCurrency)
        ?? new Currency(_options.DefaultCurrency, 2);

    private Currency? Supported(string? code) => Resolve(_options, code);

    /// <summary>The supported currency this code means, or <c>null</c> if the shop does not price in it.</summary>
    internal static Currency? Resolve(CurrencyOptions options, string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : options.Supported
                .Where(supported => string.Equals(supported.Code, code.Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(supported => new Currency(supported.Code.ToUpperInvariant(), supported.Decimals))
                .FirstOrDefault();
}

/// <summary>A fixed currency, for a consumer or a test that has no request.</summary>
public class FixedCurrency(Currency currency) : IRequestCurrency
{
    public Currency Current { get; } = currency;
}
