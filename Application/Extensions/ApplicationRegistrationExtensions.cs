using System.Reflection;
using Application.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Extensions;

public static class ApplicationRegistrationExtensions
{
    public static IServiceCollection AddApplicationRegistration(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(
            Assembly.GetAssembly(typeof(ApplicationRegistrationExtensions))
        );
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services.AddMediatR(config =>
            config.RegisterServicesFromAssemblies(
                typeof(ApplicationRegistrationExtensions).Assembly
            )
        );
    }
}
