using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Infrastructure.Persistence;
using Ecommerce.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Ecommerce.Application.Auth.TwoFactor;
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
        // Which application a sign-in comes from (specs/138): staff roles for the back office only.
        services.AddHttpContextAccessor();
        services.AddSingleton<ISessionClient, OriginSessionClient>();

        // Two-factor sign-in (#218, specs/110). No usable key refuses to start - never secrets under a known key.
        services.AddOptions<TwoFactorOptions>()
            .Bind(configuration.GetSection(TwoFactorOptions.SectionName))
            .Validate(o => !o.Problems().Any(), "TwoFactor settings are invalid: TWO_FACTOR_KEY must be 32 random bytes in base64.")
            .ValidateOnStart();
        services.AddSingleton<ITwoFactorSecretProtector, TwoFactorSecretProtector>();
        services.AddScoped<ITwoFactorRepository, TwoFactorRepository>();
        services.AddScoped<Ecommerce.Application.MyData.IPersonalDataReader, PersonalDataReader>();
        services.AddScoped<Ecommerce.Application.Auth.Commands.DeleteAccount.IAccountErasure, AccountErasure>();

        // Identity's first call out (specs/112): what keeps an account open, asked of Order over h2c on its gRPC port -
        // order:8081 in a container, localhost:5159 under start-dev.
        var orderGrpc = configuration["Order:GrpcAddress"]
            ?? Environment.GetEnvironmentVariable("ORDER_GRPC_ADDRESS")
            ?? "http://localhost:5159";
        services.AddGrpcClient<Ecommerce.Contracts.Grpc.AccountStanding.AccountStandingClient>(o => o.Address = new Uri(orderGrpc));
        services.AddHttpContextAccessor();
        services.AddScoped<Ecommerce.Application.Auth.Commands.DeleteAccount.IAccountStanding, Ecommerce.Infrastructure.Order.GrpcAccountStanding>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IAddressRepository, AddressRepository>();
        services.AddScoped<IShopApplicationRepository, ShopApplicationRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Email (specs/060): kept in outgoing_emails, sent by a sweeper over SMTP - Mailpit in development.
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.PostConfigure<SmtpOptions>(o => SmtpSettings.Apply(o, Environment.GetEnvironmentVariable));
        // A mail server that will refuse every email is found at startup, not by the first person waiting for one.
        services.AddOptions<SmtpOptions>()
            .Validate(o => o.Problems().Count == 0, "SMTP settings are invalid - see SmtpOptions.Problems.")
            .ValidateOnStart();
        services.PostConfigure<EmailOptions>(o =>
            o.StorefrontUrl = Environment.GetEnvironmentVariable("STOREFRONT_URL") ?? o.StorefrontUrl);
        services.AddScoped<IOutgoingEmailRepository, OutgoingEmailRepository>();
        services.AddScoped<Ecommerce.Application.Auth.Commands.PasswordReset.IPasswordResetRepository, PasswordResetRepository>();
        services.AddScoped<Ecommerce.Application.Auth.Handoff.IBackOfficeHandoffRepository, BackOfficeHandoffRepository>();
        services.AddSingleton<IEmailTransport, SmtpEmailTransport>();
        services.AddScoped<IEmailTemplateStore, EmailTemplateStore>();
        services.AddSingleton<IHtmlSanitizer, AllowListHtmlSanitizer>();

        // Wrong passwords counted per email (specs/062). Bad settings refuse to start rather than disable it.
        services.AddOptions<Ecommerce.Application.Auth.SignInThrottling.SignInOptions>()
            .Bind(configuration.GetSection(Ecommerce.Application.Auth.SignInThrottling.SignInOptions.SectionName))
            .Validate(o => !o.Problems().Any(), "SignIn settings are invalid: MaxFailures, WindowMinutes and CooldownMinutes must each be at least 1.")
            .ValidateOnStart();
        services.AddScoped<Ecommerce.Application.Auth.SignInThrottling.ISignInThrottle, SignInThrottleRepository>();

        // Confirming addresses (specs/063).
        services.AddScoped<Ecommerce.Application.Auth.Commands.EmailConfirmation.IEmailConfirmationRepository, EmailConfirmationRepository>();

        services.AddScoped<DataInitializer>();

        return services;
    }
}
