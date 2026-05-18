using System.Reflection;
using Application.Behaviors;
using Application.Features.Users.Services;
using Application.Shared;
using Application.Shared.Services;
using Application.Shared.Services.Files;
using Domain.Entities;
using Infrastructure.Behaviors;
using Infrastructure.Diagnostics;
using Infrastructure.Repositories.Extensions;
using Infrastructure.Seeds;
using Infrastructure.Services;
using Infrastructure.Services.Attachments;
using Infrastructure.Services.Files;
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
        services.AddMemoryCache();

        services.AddInfrastructurePipelineBehaviors();
        services.AddInfrastructureServiceRegistrations(configuration);
        return services;
    }

    public static void AddInfrastructureServiceRegistrations(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddScoped<ICurrentTenant, CurrentTenant>();
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<IAuthProvider, OtpAuthProvider>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddScoped<IAttachmentService, LocalAttachmentService>();
        services.AddScoped<ISkillCatalogService, SkillCatalogService>();
        services.AddScoped<ITeamSkillRequirementProvider, TeamSkillRequirementProvider>();
        services.AddScoped<IMatrixNotificationService, MatrixNotificationService>();
        services.AddHttpContextAccessor();
        services.AddSingleton<ExceptionCaptureBackgroundService>();
        services.AddHostedService(sp => sp.GetRequiredService<ExceptionCaptureBackgroundService>());
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

        // Seed runs as platform admin so the TenantStampingInterceptor
        // permits TenantId=null inserts for the global catalog.
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        currentTenant.Set(tenantId: null, userId: null, membershipId: null, role: null, isPlatformAdmin: true);
        SkillSeed.EnsureSeeded(db);
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
}
