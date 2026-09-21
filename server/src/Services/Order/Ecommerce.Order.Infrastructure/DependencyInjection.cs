using Ecommerce.Contracts.Grpc;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Infrastructure.Cart;
using Ecommerce.Order.Infrastructure.Catalog;
using Ecommerce.Order.Infrastructure.Persistence;
using Ecommerce.Order.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Ecommerce.Order.Infrastructure.Identity;
using Ecommerce.Order.Infrastructure.Shipping;

namespace Ecommerce.Order.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OrderDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                // depends_on waits for a container's health check, not for readiness under load.
                // A service reaching its database a moment early is normal in a container stack,
                // not a failure - it recovers instead of needing a restart by hand.
                npgsql => npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null)));

        services.AddScoped<IOrderRepository, OrderRepository>();

        // The first synchronous cross-service call in this system. Everything else is messages.
        //
        // h2c - cleartext HTTP/2 - because Catalog serves gRPC on a port of its own: one plaintext
        // port cannot carry both protocols, since telling them apart needs ALPN and ALPN is part of
        // TLS. The address is configurable so the container (catalog:8081) and start-dev
        // (localhost:5157) can differ.
        var catalogGrpc = configuration["Catalog:GrpcAddress"]
            ?? Environment.GetEnvironmentVariable("CATALOG_GRPC_ADDRESS")
            ?? "http://localhost:5157";

        services.AddGrpcClient<CatalogPricing.CatalogPricingClient>(o =>
            o.Address = new Uri(catalogGrpc));

        services.AddScoped<ICatalogPrices, GrpcCatalogPrices>();

        // The second synchronous dependency of checkout. Cart serves gRPC on its own port too.
        var cartGrpc = configuration["Cart:GrpcAddress"]
            ?? Environment.GetEnvironmentVariable("CART_GRPC_ADDRESS")
            ?? "http://localhost:5162";

        services.AddGrpcClient<CartReading.CartReadingClient>(o => o.Address = new Uri(cartGrpc));
        services.AddHttpContextAccessor();
        services.AddScoped<ICartReader, GrpcCartReader>();

        // The third (feature 011): Identity, for the delivery address. Same pattern, own port.
        var identityGrpc = configuration["Identity:GrpcAddress"]
            ?? Environment.GetEnvironmentVariable("IDENTITY_GRPC_ADDRESS")
            ?? "http://localhost:5156";

        services.AddGrpcClient<AddressReading.AddressReadingClient>(o => o.Address = new Uri(identityGrpc));
        services.AddScoped<IAddressReader, GrpcAddressReader>();

        services.AddSingleton<IShippingOptions, ConfiguredShippingOptions>();

        return services;
    }
}
