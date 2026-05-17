using Application.Shared;
using Infrastructure;
using Infrastructure.Repositories.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

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
        var dbName = $"{prefix}_{dbId.Value.ToString().Replace("-", "_")}";
        var schema = dbName;
        
        var masterConnectionString = fixture.Container.GetConnectionString();
        using (var masterConn = new NpgsqlConnection(masterConnectionString))
        {
            masterConn.Open();
            // Check if database exists
            using var checkCmd = new NpgsqlCommand($"SELECT 1 FROM pg_database WHERE datname = '{dbName}'", masterConn);
            if (checkCmd.ExecuteScalar() == null)
            {
                using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{dbName}\"", masterConn);
                createCmd.ExecuteNonQuery();
            }
        }

        var connectionString =
            fixture
                .Container.GetConnectionString()
                .Replace(
                    PostgreSqlRepositoryTestDatabaseFixture.DefaultDbName,
                    dbName
                ) + $";Search Path={schema}";
        
        services.AddDbContext<TDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });
        services.AddScoped<TDbContext>(sp =>
        {
            var options = sp.GetRequiredService<DbContextOptions<TDbContext>>();
            var currentTenant = sp.GetRequiredService<ICurrentTenant>();
            return (TDbContext)Activator.CreateInstance(typeof(TDbContext), options, currentTenant, schema)!;
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
