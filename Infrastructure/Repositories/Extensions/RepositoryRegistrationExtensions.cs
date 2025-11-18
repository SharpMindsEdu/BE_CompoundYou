using System.Reflection;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Infrastructure.Repositories.Extensions;

public static class RepositoryRegistrationExtensions
{
    public static IServiceCollection AddRepositories<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.AddSpecifications();
        services.TryAddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.TryAddScoped<IDbContextResolver, DbContextResolver>();
        services.AddScoped<IDbContextFactory, DbContextFactory<TDbContext>>();

        return services;
    }

    private static IServiceCollection AddSpecifications(this IServiceCollection services)
    {
        var specificationType = typeof(ISpecification<>);
        var specificationInterfaces = specificationType
            .Assembly.GetTypes()
            .Where(t =>
                t.IsInterface
                && t.GetInterfaces()
                    .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == specificationType)
            )
            .ToList();
        var implementations = Assembly
            .GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Select(t => new
            {
                Implementation = t,
                Interface = t.GetInterfaces()
                    .FirstOrDefault(i => specificationInterfaces.Contains(i)),
            })
            .Where(x => x.Interface != null);

        foreach (var impl in implementations)
        {
            services.AddScoped(impl.Interface!, impl.Implementation);
        }

        return services;
    }
}
