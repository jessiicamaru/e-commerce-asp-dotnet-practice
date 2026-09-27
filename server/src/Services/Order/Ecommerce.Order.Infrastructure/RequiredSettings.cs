using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Infrastructure.Shipping;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Infrastructure;

/// <summary>
/// Order's required settings, resolved at startup (constitution: Configuration). Each validates in its constructor, so
/// resolving it here stops the service where the log says why, instead of turning every checkout into a 500.
/// </summary>
public static class RequiredSettings
{
    public static void Check(IServiceProvider services)
    {
        // Since specs/098 the shipping options only seed the table checkout reads - they are still validated here.
        _ = services.GetRequiredService<ConfiguredShippingOptions>();
        _ = services.GetRequiredService<ITaxRates>();
        // Until specs/103 (#210) this one was found at the first checkout, as a 500.
        _ = services.GetRequiredService<ICommissionRate>();
    }
}
