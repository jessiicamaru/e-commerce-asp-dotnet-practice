using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Infrastructure.Persistence;
using Ecommerce.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Ecommerce.Application.Email;
using Ecommerce.Infrastructure.Email;
using Ecommerce.Infrastructure.Security;
using Ecommerce.Shared.Authentication;

namespace Ecommerce.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                // depends_on waits for a container's health check, not for readiness under load.
                // A service reaching its database a moment early is normal in a container stack,
                // not a failure - it recovers instead of needing a restart by hand.
                npgsql => npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null)));

        services.Configure<JwtSettings>(
            configuration.GetSection(JwtSettings.SectionName)
        );

        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IAddressRepository, AddressRepository>();
        services.AddScoped<IShopApplicationRepository, ShopApplicationRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Email (specs/060): kept in outgoing_emails, sent by a sweeper over SMTP - Mailpit in development.
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.PostConfigure<SmtpOptions>(o =>
        {
            o.SmtpHost = Environment.GetEnvironmentVariable("SMTP_HOST") ?? o.SmtpHost;
            if (int.TryParse(Environment.GetEnvironmentVariable("SMTP_PORT"), out var port)) o.SmtpPort = port;
        });
        services.PostConfigure<EmailOptions>(o =>
            o.StorefrontUrl = Environment.GetEnvironmentVariable("STOREFRONT_URL") ?? o.StorefrontUrl);
        services.AddScoped<IOutgoingEmailRepository, OutgoingEmailRepository>();
        services.AddSingleton<IEmailTransport, SmtpEmailTransport>();

        services.AddScoped<DataInitializer>();

        return services;
    }
}
