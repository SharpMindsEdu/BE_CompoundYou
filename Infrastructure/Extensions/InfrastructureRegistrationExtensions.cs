using Application.Features.Users.Services;
using Domain.Entities;
using Application.Shared.Services.Files;
using Domain.Services.Ai;
using Domain.Services.Riftbound;
using Infrastructure.Behaviors;
using Infrastructure.Repositories.Extensions;
using Infrastructure.Services;
using Infrastructure.Services.Ai;
using Infrastructure.Services.Attachments;
using Infrastructure.Services.Files;
using Infrastructure.Services.Riftbound;
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
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.Database.ExecuteSqlRaw(
            """
            INSERT INTO "public"."user" (
                "id",
                "display_name",
                "email",
                "phone_number",
                "sign_in_secret",
                "sign_in_tries",
                "created_on",
                "updated_on",
                "deleted_on"
            )
            VALUES (
                2,
                'SkyTest',
                'test@test.de',
                '+491515632156',
                'skyt',
                1,
                '-infinity',
                '-infinity',
                '-infinity'
            )
            ON CONFLICT ("id")
            DO UPDATE SET
                "display_name" = EXCLUDED."display_name",
                "email" = EXCLUDED."email",
                "phone_number" = EXCLUDED."phone_number",
                "sign_in_secret" = EXCLUDED."sign_in_secret",
                "sign_in_tries" = EXCLUDED."sign_in_tries",
                "created_on" = EXCLUDED."created_on",
                "updated_on" = EXCLUDED."updated_on",
                "deleted_on" = EXCLUDED."deleted_on";
            """
        );

        db.Database.ExecuteSqlRaw(
            """
            SELECT setval(
                pg_get_serial_sequence('"public"."user"', 'id'),
                (SELECT COALESCE(MAX("id"), 1) FROM "public"."user"),
                true
            );
            """
        );

        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var scalarDebugUser = new User
        {
            Id = 2,
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
}
