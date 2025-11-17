using Application.Features.Users.Services;
using Application.Shared.Services.Files;
using Domain.Services.Riftbound;
using Infrastructure.Behaviors;
using Infrastructure.Repositories.Extensions;
using Infrastructure.Services;
using Infrastructure.Services.Attachments;
using Infrastructure.Services.Files;
using Infrastructure.Services.Riftbound;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddScoped<IRiftboundCardService, RiftboundCardService>();
        services.AddInfrastructurePipelineBehaviors();
        services.AddInfrastructureServiceRegistrations();
        return services;
    }

    public static void AddInfrastructureServiceRegistrations(this IServiceCollection services)
    {
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddScoped<IAttachmentService, LocalAttachmentService>();
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
}
