using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace Ecommerce.Shared.Money;

public static class DependencyInjection
{
    /// <summary>
    /// Registers currency resolution and <see cref="IRequestCurrency"/> (specs/022). Call it in any
    /// service whose responses carry an amount of money.
    /// </summary>
    public static IServiceCollection AddRequestCurrency(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CurrencyOptions>(configuration.GetSection(CurrencyOptions.SectionName));

        var options = configuration.GetSection(CurrencyOptions.SectionName).Get<CurrencyOptions>() ?? new CurrencyOptions();

        if (options.Supported.Length == 0)
        {
            throw new InvalidOperationException(
                $"'{CurrencyOptions.SectionName}:Supported' is empty. This service could not price anything.");
        }

        var duplicate = options.Supported
            .GroupBy(currency => currency.Code?.Trim().ToUpperInvariant())
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Currency '{duplicate.Key}' is configured twice.");
        }

        var negative = options.Supported.FirstOrDefault(currency => currency.Decimals < 0);

        if (negative is not null)
        {
            throw new InvalidOperationException(
                $"Currency '{negative.Code}' has {negative.Decimals} decimal places. A currency has none or more.");
        }

        // A default that is not in the supported list would make every request fall back to a currency
        // nothing can price in - worth refusing at startup rather than on every read.
        if (RequestCurrency.Resolve(options, options.DefaultCurrency) is null)
        {
            throw new InvalidOperationException(
                $"'{CurrencyOptions.SectionName}:DefaultCurrency' is '{options.DefaultCurrency}', which is not in "
                + $"Supported ({string.Join(", ", options.Supported.Select(currency => currency.Code))}). Every request "
                + "would fall back to a currency this service cannot price in.");
        }

        services.AddHttpContextAccessor();
        services.AddScoped<IRequestCurrency, RequestCurrency>();

        return services;
    }

    /// <summary>
    /// Says which currency the response is in, and that the answer depends on being asked.
    /// </summary>
    /// <remarks>
    /// <b><c>Vary: X-Currency</c> is not decoration</b>, for the same reason
    /// <c>Vary: Accept-Language</c> is not: without it a cache between here and the customer may serve
    /// one shopper's dong prices to the next shopper asking in dollars, because the URL is identical
    /// and nothing said the answer depended on a header. A price is the worst thing in the shop to
    /// serve from the wrong cache entry.
    /// </remarks>
    public static IApplicationBuilder UseRequestCurrency(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var currency = context.RequestServices.GetRequiredService<IRequestCurrency>().Current;

            context.Response.Headers[RequestCurrency.HeaderName] = currency.Code;
            context.Response.Headers.Vary = StringValues.Concat(
                context.Response.Headers.Vary, RequestCurrency.HeaderName);

            await next();
        });
}
