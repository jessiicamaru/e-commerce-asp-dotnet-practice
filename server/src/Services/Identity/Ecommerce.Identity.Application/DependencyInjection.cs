using Ecommerce.Shared.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddValidatorsFromAssembly(assembly);

        // Stages a confirmation link with whatever save the caller makes (specs/063).
        services.AddScoped<Auth.Commands.EmailConfirmation.EmailConfirmations>();

        // An email's current words - an administrator's, or the built-in ones (specs/077).
        services.AddScoped<Email.EmailComposer>();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        return services;
    }
}
