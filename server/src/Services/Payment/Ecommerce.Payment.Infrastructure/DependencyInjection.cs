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
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.Configure<PaymentOutcomeOptions>(
            configuration.GetSection(PaymentOutcomeOptions.SectionName));

        services.AddSingleton<IPaymentGateway, StubPaymentGateway>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();

        return services;
    }
}
