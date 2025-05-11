using DotNet.Testcontainers.Configurations;
using Testcontainers.PostgreSql;
using Unit.Tests;

[assembly: AssemblyFixture(typeof(PostgreSqlRepositoryTestDatabaseFixture))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Unit.Tests;

public class PostgreSqlRepositoryTestDatabaseFixture : IAsyncLifetime
{
    public static string DefaultDbName = "";
    private const string DatabaseUsername = "root";
    private const string DatabasePassword = "rootpw";
    public readonly PostgreSqlContainer Container;

    public PostgreSqlRepositoryTestDatabaseFixture()
    {
        DefaultDbName = Guid.NewGuid().ToString();
        var initScriptPath = Path.GetFullPath("init.sql");

        Container = new PostgreSqlBuilder()
            .WithImage("postgres:16")
            .WithName(DefaultDbName)
            .WithUsername(DatabaseUsername)
            .WithPassword(DatabasePassword)
            .WithBindMount(initScriptPath, "/docker-entrypoint-initdb.d/init.sql", AccessMode.ReadOnly)
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
