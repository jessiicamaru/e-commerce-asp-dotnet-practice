using Ecommerce.Cart.Application.Common.Interfaces;
using Ecommerce.Cart.Infrastructure.Catalog;
using Ecommerce.Cart.Infrastructure.Persistence;
using Ecommerce.Cart.Infrastructure.Persistence.Repositories;
using Ecommerce.Contracts.Grpc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Cart.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CartDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                // A service reaching its database a moment early is normal in a container stack.
                npgsql => npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null)));

        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<ICheckoutOutcomeRepository, CheckoutOutcomeRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Catalog serves gRPC on its own port (h2c): one plaintext port cannot carry HTTP/1.1 and
        // HTTP/2. See specs/009-catalog-owns-price.
        var catalogGrpc = configuration["Catalog:GrpcAddress"]
            ?? Environment.GetEnvironmentVariable("CATALOG_GRPC_ADDRESS")
            ?? "http://localhost:5157";

        services.AddGrpcClient<CatalogPricing.CatalogPricingClient>(o => o.Address = new Uri(catalogGrpc));
        services.AddScoped<ICatalogProducts, GrpcCatalogProducts>();

        return services;
    }
}
