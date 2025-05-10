using Infrastructure;
using Infrastructure.Repositories.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Unit.Tests;

public static class DbUtils
{
    public static string RegisterTestDbContext<TDbContext>(
        this IServiceCollection services,
        PostgreSqlRepositoryTestDatabaseFixture fixture,
        string? prefix = "T",
        Guid? dbId = null
    )
        where TDbContext : DbBaseContext
    {
        dbId ??= Guid.NewGuid();
        var schema = $"{prefix}_{dbId.Value.ToString().Replace("-", "_")}"; // Schema-Namen dürfen keine `-` enthalten
        var connectionString =
            fixture
                .Container.GetConnectionString()
                .Replace(
                    PostgreSqlRepositoryTestDatabaseFixture.DefaultDbName,
                    $"{prefix}_{dbId.ToString()}"
                ) + $";Search Path={schema}";

        services.AddDbContext<TDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });
        services.AddScoped<TDbContext>(sp =>
        {
            var options = sp.GetRequiredService<DbContextOptions<TDbContext>>();
            return (TDbContext)Activator.CreateInstance(typeof(TDbContext), options, schema)!;
        });
        services.AddRepositories<TDbContext>();

        using var scope = services.BuildServiceProvider().CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
#pragma warning disable EF1002
        db.Database.ExecuteSqlRaw($"CREATE SCHEMA IF NOT EXISTS \"{schema}\";");
#pragma warning restore EF1002

        return schema;
    }

    public static void RegisterTestDbContext<TDbContext>(
        this IServiceCollection services,
        string? prefix = "",
        Guid? dbId = null
    )
        where TDbContext : DbContext
    {
        dbId ??= Guid.NewGuid();
        services.AddDbContext<TDbContext>(options =>
        {
            options.UseInMemoryDatabase($"{prefix}_{dbId.ToString()}");
        });
        services.AddRepositories<TDbContext>();
    }
}
