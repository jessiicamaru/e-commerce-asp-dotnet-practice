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

        services.Configure<VnPayOptions>(configuration.GetSection(VnPayOptions.SectionName));

        // Always there, so an IPN reaching a service that is not using VNPay is refused (97) rather than unrouted -
        // unconfigured, it believes nothing (specs/143).
        services.AddSingleton<IVnPay, VnPaySignature>();

        var provider = configuration[$"{PaymentOutcomeOptions.SectionName}:Provider"]?.Trim();
        if (string.IsNullOrEmpty(provider) || string.Equals(provider, PaymentOutcomeOptions.StubProviderValue, StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IPaymentGateway, StubPaymentGateway>();
        }
        else if (string.Equals(provider, PaymentOutcomeOptions.VnPayProviderValue, StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IPaymentGateway, VnPayGateway>();

            // Settings that could only fail at the first customer are refused at startup instead.
            var sagaTimeout = int.TryParse(configuration["ORCHESTRATOR_PAYMENT_TIMEOUT_SECONDS"], out var seconds) ? seconds : (int?)null;
            services.AddOptions<VnPayOptions>()
                .Validate(o => o.Problems(sagaTimeout).Count == 0, "VNPay settings are invalid - see VnPayOptions.Problems.")
                .ValidateOnStart();
        }
        else
        {
            // A typo silently read as the stub would let a stand-in fulfil orders somebody believed were paid.
            throw new InvalidOperationException($"PAYMENT_PROVIDER is '{provider}', which is neither 'Stub' nor 'VnPay'.");
        }

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<Ecommerce.Payment.Application.MyData.IPersonalDataReader, PersonalDataReader>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();

        return services;
    }
}
