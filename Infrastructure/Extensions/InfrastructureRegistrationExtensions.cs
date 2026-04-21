using Application.Features.Trading.Automation;
using Application.Features.Users.Services;
using Application.Shared.Services.Files;
using Domain.Entities;
using Domain.Services.Ai;
using Domain.Services.Riftbound;
using Domain.Services.Trading;
using Infrastructure.Behaviors;
using Infrastructure.Repositories.Extensions;
using Infrastructure.Services;
using Infrastructure.Services.Ai;
using Infrastructure.Services.Attachments;
using Infrastructure.Services.Files;
using Infrastructure.Services.Riftbound;
using Infrastructure.Services.Trading;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Extensions;

public static class InfrastructureRegistrationExtensions
{
    public static IServiceCollection AddInfrastructureRegistration(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        services.AddRepositories<ApplicationDbContext>();
        services.AddHttpClient();

        services.AddInfrastructurePipelineBehaviors();
        services.AddInfrastructureServiceRegistrations(configuration);
        return services;
    }

    public static void AddInfrastructureServiceRegistrations(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddScoped<IAttachmentService, LocalAttachmentService>();
        services.AddScoped<IRiftboundCardService, RiftboundCardService>();

        services.Configure<RiftboundAiModelOptions>(
            configuration.GetSection(RiftboundAiModelOptions.SectionName)
        );
        services.Configure<AlpacaTradingOptions>(
            configuration.GetSection(AlpacaTradingOptions.SectionName)
        );
        services.Configure<OpenAiTradingOptions>(
            configuration.GetSection(OpenAiTradingOptions.SectionName)
        );
        services.Configure<TradingAutomationOptions>(
            configuration.GetSection(TradingAutomationOptions.SectionName)
        );

        services.AddHttpClient<ITradingDataProvider, AlpacaTradingDataProvider>();
        services.AddHttpClient<ITradingAgentRuntime, OpenAiTradingAgentRuntime>();
        services.AddScoped<ITradingAgentOrchestrator, TradingAgentOrchestrator>();

        RegisterTradingAgents(services, configuration);

        services.AddSingleton<EmbeddedRiftboundAiModelService>();
        services.AddSingleton<IRiftboundAiModelService>(sp =>
            sp.GetRequiredService<EmbeddedRiftboundAiModelService>());
        services.AddSingleton<IRiftboundTrainingDataStore>(sp =>
            sp.GetRequiredService<EmbeddedRiftboundAiModelService>());
        services.AddSingleton<IRiftboundAiOnlineTrainer>(sp =>
            sp.GetRequiredService<EmbeddedRiftboundAiModelService>());
    }

    public static void AddInfrastructureServiceRegistrations(this IServiceCollection services)
    {
        var defaultConfiguration = new ConfigurationBuilder().Build();
        services.AddInfrastructureServiceRegistrations(defaultConfiguration);
    }

    public static void AddInfrastructurePipelineBehaviors(this IServiceCollection services)
    {
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
    }

    public static void ExecuteMigrations(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate();
    }

    public static string? ConfigureDebugScalarAuthorization(this WebApplication app)
    {
#if DEBUG
        if (!app.Environment.IsDevelopment())
            return null;

        using var scope = app.Services.CreateScope();

        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var scalarDebugUser = new User
        {
            Id = 0,
            DisplayName = "SkyTest",
            Email = "test@test.de",
            PhoneNumber = "+491515632156",
            SignInSecret = "skyt",
            SignInTries = 1,
            CreatedOn = DateTimeOffset.MinValue,
            UpdatedOn = DateTimeOffset.MinValue,
            DeletedOn = DateTimeOffset.MinValue,
        };

        return tokenService.CreateToken(scalarDebugUser);
#else
        return null;
#endif
    }

    private static void RegisterTradingAgents(IServiceCollection services, IConfiguration configuration)
    {
        var options = new TradingAutomationOptions();
        configuration.GetSection(TradingAutomationOptions.SectionName).Bind(options);

        foreach (var agent in options.Agents.Where(x =>
                     !string.IsNullOrWhiteSpace(x.Name)
                     && !string.IsNullOrWhiteSpace(x.SystemPrompt)))
        {
            services.AddScoped<ITradingAgent>(sp =>
                new OpenAiTradingAgent(
                    agent.Name.Trim(),
                    agent.SystemPrompt.Trim(),
                    sp.GetRequiredService<ITradingAgentRuntime>()
                )
            );
        }
    }
}
