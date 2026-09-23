using Ecommerce.Inventory.Infrastructure.Catalog;
using Ecommerce.Contracts.Grpc;
using Ecommerce.Inventory.Application.Common.Interfaces;
using Ecommerce.Inventory.Infrastructure.Persistence;
using Ecommerce.Inventory.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Inventory.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<InventoryDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                // depends_on waits for a container's health check, not for readiness under load.
                // A service reaching its database a moment early is normal in a container stack,
                // not a failure - it recovers instead of needing a restart by hand.
                npgsql => npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IReservationRepository, ReservationRepository>();

        // Inventory's FIRST synchronous dependency on another service (specs/031). Everything it
        // did before this was messages.
        //
        // h2c on a port of its own, like every gRPC edge here: one plaintext port cannot carry
        // HTTP/1.1 and HTTP/2, because telling them apart needs ALPN and ALPN is part of TLS. The
        // address is configurable so the container (catalog:8081) and start-dev (localhost:5157)
        // can differ.
        var catalogGrpc = configuration["Catalog:GrpcAddress"]
            ?? Environment.GetEnvironmentVariable("CATALOG_GRPC_ADDRESS")
            ?? "http://localhost:5157";

        services.AddGrpcClient<CatalogOwnership.CatalogOwnershipClient>(o =>
            o.Address = new Uri(catalogGrpc));

        services.AddScoped<IProductOwnership, GrpcProductOwnership>();

        return services;
    }
}
