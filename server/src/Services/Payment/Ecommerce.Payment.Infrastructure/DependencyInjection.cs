using Ecommerce.Payment.Application.Common.Interfaces;
using Ecommerce.Payment.Infrastructure.Gateway;
using Ecommerce.Payment.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Payment.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PaymentDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                // depends_on waits for a container's health check, not for readiness under load.
                // A service reaching its database a moment early is normal in a container stack,
                // not a failure - it recovers instead of needing a restart by hand.
                npgsql => npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null)));

        services.Configure<PaymentOutcomeOptions>(
            configuration.GetSection(PaymentOutcomeOptions.SectionName));

        services.AddSingleton<IPaymentGateway, StubPaymentGateway>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();

        return services;
    }
}
