using DotNet.Testcontainers.Configurations;
using Testcontainers.PostgreSql;

namespace Unit.Tests;

public class PostgreSqlRepositoryTestDatabaseFixture : IAsyncLifetime
{
    public const string DefaultDbName = "postgres";
    private const string DatabaseUsername = "root";
    private const string DatabasePassword = "rootpw";
    public readonly PostgreSqlContainer Container;

    public PostgreSqlRepositoryTestDatabaseFixture()
    {
        var initScriptPath = Path.GetFullPath("init.sql");

        Container = new PostgreSqlBuilder()
            .WithImage("postgres:16")
            .WithDatabase(DefaultDbName)
            .WithUsername(DatabaseUsername)
            .WithPassword(DatabasePassword)
            .WithBindMount(
                initScriptPath,
                "/docker-entrypoint-initdb.d/init.sql",
                AccessMode.ReadOnly
            )
            .Build();
    }

    public async ValueTask InitializeAsync()
    {
        await Container.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Container.StopAsync();
    }
}
