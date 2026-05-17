using DotNet.Testcontainers.Configurations;
using Testcontainers.PostgreSql;

namespace Unit.Tests;

public class PostgreSqlRepositoryTestDatabaseFixture : IAsyncLifetime
{
    public const string DefaultDbName = "postgres";
    private const string DatabaseUsername = "root";
    private const string DatabasePassword = "rootpw";

    private static readonly PostgreSqlContainer _container;
    private static readonly Task _startTask;

    static PostgreSqlRepositoryTestDatabaseFixture()
    {
        var initScriptPath = Path.GetFullPath("init.sql");

        _container = new PostgreSqlBuilder()
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

        _startTask = _container.StartAsync();
    }

    public PostgreSqlContainer Container => _container;

    public async ValueTask InitializeAsync()
    {
        await _startTask;
    }

    public ValueTask DisposeAsync()
    {
        // We don't stop the container here because other parallel tests might still be using it.
        // It will be stopped when the process exits or by the test runner cleanup if supported.
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Explicitly stop the container if needed (e.g. in a global cleanup).
    /// </summary>
    public static async Task StopContainerAsync()
    {
        await _container.StopAsync();
    }
}
