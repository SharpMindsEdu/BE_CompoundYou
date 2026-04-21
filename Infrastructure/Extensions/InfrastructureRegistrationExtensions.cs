using Application.Features.Users.Services;
using Domain.Entities;
using Application.Shared.Services.Files;
using Infrastructure.Behaviors;
using Infrastructure.Repositories.Extensions;
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
}
