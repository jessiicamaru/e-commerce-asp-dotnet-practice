using Ecommerce.Contracts.Grpc;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Infrastructure.Catalog;
using Ecommerce.Order.Infrastructure.Persistence;
using Ecommerce.Order.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        return services;
    }
}
