using System.Reflection;
using Application.Behaviors;
using Application.Features.Riftbound.DeckOptimization.Services;
using Application.Features.Riftbound.BackgroundServices;
using Application.Features.Riftbound.Simulation.Engine;
using Application.Features.Riftbound.Simulation.Policies;
using Application.Features.Riftbound.Simulation.Services;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Extensions;

public static class ApplicationRegistrationExtensions
{
    public static IServiceCollection AddApplicationRegistration(this IServiceCollection services)
    {
        services.AddHostedService<RiftboundCardUpdater>();
        services.AddScoped<IRiftboundSimulationEngine, RiftboundSimulationEngine>();
        services.AddSingleton<
            IRiftboundSimulationDefinitionRegistry,
            FileBackedRiftboundSimulationDefinitionRegistry
        >();
        services.AddScoped<
            IRiftboundDeckSimulationReadinessService,
            RiftboundDeckSimulationReadinessService
        >();
        services.AddScoped<HeuristicMovePolicy>();
        services.AddScoped<LlmMovePolicy>();
        services.AddScoped<IMovePolicy>(sp => sp.GetRequiredService<HeuristicMovePolicy>());
        services.AddScoped<IMovePolicy>(sp => sp.GetRequiredService<LlmMovePolicy>());
        services.AddScoped<IMovePolicyResolver, MovePolicyResolver>();
        services.AddScoped<IRiftboundSimulationService, RiftboundSimulationService>();
        services.AddSingleton<IRiftboundDeckOptimizationRunQueue, RiftboundDeckOptimizationRunQueue>();
        services.AddHostedService<RiftboundDeckOptimizationWorker>();
        services.AddScoped<RiftboundDeckOptimizationService>();
        services.AddScoped<IRiftboundDeckOptimizationService>(sp =>
            sp.GetRequiredService<RiftboundDeckOptimizationService>());
        services.AddScoped<IRiftboundDeckOptimizationRunExecutor>(sp =>
            sp.GetRequiredService<RiftboundDeckOptimizationService>());

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
