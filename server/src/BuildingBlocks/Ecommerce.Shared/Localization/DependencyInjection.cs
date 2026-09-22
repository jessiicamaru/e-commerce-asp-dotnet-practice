using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Ecommerce.Shared.Localization;

public static class DependencyInjection
{
    /// <summary>
    /// Registers language negotiation and <see cref="IRequestLanguage"/> (specs/021). Call it in any
    /// service whose responses carry text a customer reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The negotiation is ASP.NET Core's, not ours.</b> <c>RequestLocalizationMiddleware</c> already
    /// parses <c>Accept-Language</c> properly - quality values, ordering, and matching a region tag
    /// (<c>vi-VN</c>) to a supported language (<c>vi</c>). A hand-rolled parser that walks the header in
    /// the order it arrives is right most of the time and wrong for a client that sends
    /// <c>en;q=0.4,vi;q=0.9</c>, which is legal and means Vietnamese.
    /// </para>
    /// <para>
    /// <see cref="IRequestLanguage"/> stays, because the Application layer must not read
    /// <c>HttpContext</c> or ambient <c>CultureInfo</c> - it asks, exactly as it asks
    /// <c>ICurrentUser</c> who the caller is.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddRequestLanguage(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LanguageOptions>(configuration.GetSection(LanguageOptions.SectionName));

        var options = configuration.GetSection(LanguageOptions.SectionName).Get<LanguageOptions>() ?? new LanguageOptions();

        if (options.Supported.Length == 0)
        {
            throw new InvalidOperationException(
                $"'{LanguageOptions.SectionName}:Supported' is empty. This service could not answer in any language.");
        }

        // A default that is not in the supported list would make every request fall back to a language
        // nothing can serve - worth refusing at startup rather than on every read.
        if (!options.Supported.Contains(options.DefaultLanguage, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"'{LanguageOptions.SectionName}:DefaultLanguage' is '{options.DefaultLanguage}', which is not in "
                + $"Supported ({string.Join(", ", options.Supported)}). Every request would fall back to a language "
                + "this service cannot answer in.");
        }

        var supported = options.Supported.Select(language => new CultureInfo(language)).ToList();

        services.Configure<RequestLocalizationOptions>(localization =>
        {
            localization.DefaultRequestCulture = new RequestCulture(options.DefaultLanguage);
            localization.SupportedCultures = supported;
            localization.SupportedUICultures = supported;

            // vi-VN is Vietnamese. Without this a region tag nobody listed falls through to the default.
            localization.FallBackToParentCultures = true;
            localization.FallBackToParentUICultures = true;

            localization.RequestCultureProviders =
            [
                // ?lang=vi first, so a link carries the language it was written in.
                new QueryStringRequestCultureProvider { QueryStringKey = "lang", UIQueryStringKey = "lang" },
                new AcceptLanguageHeaderRequestCultureProvider(),
            ];
        });

        services.AddHttpContextAccessor();
        services.AddScoped<IRequestLanguage, RequestLanguage>();

        return services;
    }

    /// <summary>
    /// Negotiates the language, then says which one the response is in.
    /// </summary>
    /// <remarks>
    /// <b><c>Vary: Accept-Language</c> is not decoration.</b> Without it, any cache between here and the
    /// customer - a browser, a CDN, a reverse proxy - may serve one shopper's Vietnamese response to
    /// the next shopper asking in English, because the URL is identical and nothing said the answer
    /// depends on a header.
    /// </remarks>
    public static IApplicationBuilder UseRequestLanguage(this IApplicationBuilder app)
    {
        app.UseRequestLocalization();

        return app.Use(async (context, next) =>
        {
            var language = context.RequestServices.GetRequiredService<IRequestLanguage>().Current;

            context.Response.Headers.ContentLanguage = language;
            context.Response.Headers.Vary = Microsoft.Extensions.Primitives.StringValues.Concat(
                context.Response.Headers.Vary, "Accept-Language");

            await next();
        });
    }
}
