using System.Net;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Networks;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Integration.Tests.Infrastructure;

public sealed class IntegrationTestStackFixture : IAsyncLifetime
{
    private const ushort ApiPort = 8080;
    private const string DatabaseName = "compoundyou_integration";
    private const string DatabaseUsername = "postgresUser";
    private const string DatabasePassword = "postgresPW";
    private const string PostgresNetworkAlias = "postgres";
    private const string UploadPath = "/tmp/compoundyou-integration-uploads";
    private const string ApiPublishDirectoryEnvVar = "COMPOUNDYOU_INTEGRATION_API_PUBLISH_DIR";

    private readonly string _resourceSuffix = Guid.NewGuid().ToString("N");
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private bool _started;
    private INetwork? _network;
    private IFutureDockerImage? _apiImage;

    public PostgreSqlContainer PostgreSqlContainer { get; private set; } = null!;
    public IContainer ApiContainer { get; private set; } = null!;
    public Uri ApiBaseAddress { get; private set; } = null!;
    public bool IsStarted => _started;

    public string DatabaseConnectionString => PostgreSqlContainer.GetConnectionString();

    public HttpClient CreateHttpClient()
    {
        return new HttpClient
        {
            BaseAddress = ApiBaseAddress,
            Timeout = TimeSpan.FromSeconds(30),
        };
    }

    public ValueTask InitializeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public async Task EnsureStartedAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
        {
            return;
        }

        await _startLock.WaitAsync(cancellationToken);
        try
        {
            if (_started)
            {
                return;
            }

            await StartAsync(cancellationToken);
            _started = true;
        }
        finally
        {
            _startLock.Release();
        }
    }

    private async Task StartAsync(CancellationToken cancellationToken)
    {
        var repositoryRoot = FindRepositoryRoot();

        try
        {
            ConfigureReadableDockerConfig(repositoryRoot);
            _network = CreateNetwork();
            _apiImage = CreateApiImage(repositoryRoot);
            PostgreSqlContainer = CreatePostgreSqlContainer(_network);
            ApiContainer = CreateApiContainer(_network, _apiImage);

            await _network.CreateAsync(cancellationToken);
            await _apiImage.CreateAsync(cancellationToken);
            await PostgreSqlContainer.StartAsync(cancellationToken);
            await ApiContainer.StartAsync(cancellationToken);

            ApiBaseAddress = new Uri(
                $"http://{ApiContainer.Hostname}:{ApiContainer.GetMappedPublicPort(ApiPort)}/"
            );
        }
        catch (Exception ex)
        {
            if (IsDockerUnavailable(ex))
            {
                throw new InvalidOperationException(
                    "Docker is not available for Integration.Tests. Start Docker Desktop or run the tests with a user that can access the Docker daemon.",
                    ex
                );
            }

            var apiLogs = ApiContainer is null
                ? "API container was not created."
                : await TryReadContainerLogs(ApiContainer, cancellationToken);
            throw new InvalidOperationException(
                $"Docker integration test stack failed to start.{Environment.NewLine}{apiLogs}",
                ex
            );
        }
    }

    private INetwork CreateNetwork()
    {
        return new NetworkBuilder()
            .WithName($"compoundyou-integration-{_resourceSuffix}")
            .WithCleanUp(true)
            .Build();
    }

    private IFutureDockerImage CreateApiImage(string repositoryRoot)
    {
        var apiPublishDirectory = GetApiPublishDirectory(repositoryRoot);

        return new ImageFromDockerfileBuilder()
            .WithName($"compoundyou-api-integration:{_resourceSuffix}")
            .WithDockerfile("Dockerfile")
            .WithDockerfileDirectory(apiPublishDirectory)
            .WithDeleteIfExists(true)
            .WithCleanUp(true)
            .Build();
    }

    private PostgreSqlContainer CreatePostgreSqlContainer(INetwork network)
    {
        return new PostgreSqlBuilder()
            .WithImage("postgres:16")
            .WithDatabase(DatabaseName)
            .WithUsername(DatabaseUsername)
            .WithPassword(DatabasePassword)
            .WithNetwork(network)
            .WithNetworkAliases(PostgresNetworkAlias)
            .WithCleanUp(true)
            .Build();
    }

    private IContainer CreateApiContainer(INetwork network, IFutureDockerImage apiImage)
    {
        return new ContainerBuilder()
            .WithImage(apiImage)
            .WithName($"compoundyou-api-integration-{_resourceSuffix}")
            .WithNetwork(network)
            .DependsOn(PostgreSqlContainer)
            .WithPortBinding(ApiPort, true)
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Integration")
            .WithEnvironment("ASPNETCORE_URLS", $"http://+:{ApiPort}")
            .WithEnvironment(
                "ConnectionStrings__DefaultConnection",
                $"Host={PostgresNetworkAlias};Port=5432;Database={DatabaseName};Username={DatabaseUsername};Password={DatabasePassword}"
            )
            .WithEnvironment("Jwt__Issuer", "CompoundYouIntegrationTests")
            .WithEnvironment("Jwt__Audience", "CompoundYouIntegrationTests")
            .WithEnvironment("Jwt__Key", "compoundyou-integration-tests-secret-key")
            .WithEnvironment("LocalFileStorage__Path", UploadPath)
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(strategy =>
                        strategy
                            .ForPort(ApiPort)
                            .ForPath("/openapi/v1.json")
                            .ForStatusCode(HttpStatusCode.OK)
                    )
            )
            .WithCleanUp(true)
            .Build();
    }

    public async ValueTask DisposeAsync()
    {
        if (ApiContainer is not null)
        {
            await ApiContainer.DisposeAsync();
        }

        if (PostgreSqlContainer is not null)
        {
            await PostgreSqlContainer.DisposeAsync();
        }

        if (_network is not null)
        {
            await _network.DisposeAsync();
        }

        if (_apiImage is not null)
        {
            await _apiImage.DeleteAsync(CancellationToken.None);
        }

        _startLock.Dispose();
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        if (!_started)
        {
            return;
        }

        await ResetDatabaseAsync(cancellationToken);
        await ResetUploadsAsync(cancellationToken);
    }

    private async Task ResetDatabaseAsync(CancellationToken cancellationToken)
    {
        const string tableSql = """
            SELECT COALESCE(string_agg(format('%I.%I', schemaname, tablename), ', '), '')
            FROM pg_tables
            WHERE schemaname = 'public'
              AND tablename <> '__EFMigrationsHistory';
            """;

        await using var connection = new NpgsqlConnection(DatabaseConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var tableCommand = new NpgsqlCommand(tableSql, connection);
        var tableList = (string?)await tableCommand.ExecuteScalarAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(tableList))
        {
            return;
        }

        await using var truncateCommand = new NpgsqlCommand(
            $"TRUNCATE TABLE {tableList} RESTART IDENTITY CASCADE;",
            connection
        );
        await truncateCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task ResetUploadsAsync(CancellationToken cancellationToken)
    {
        var result = await ApiContainer.ExecAsync(
            ["sh", "-c", $"rm -rf {UploadPath} && mkdir -p {UploadPath}"],
            cancellationToken
        );

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to reset API upload directory. stdout: {result.Stdout} stderr: {result.Stderr}"
            );
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CompoundYou.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root containing CompoundYou.sln.");
    }

    private static string GetApiPublishDirectory(string repositoryRoot)
    {
        var configuredDirectory = Environment.GetEnvironmentVariable(ApiPublishDirectoryEnvVar);
        var publishDirectory = string.IsNullOrWhiteSpace(configuredDirectory)
            ? Path.Combine(repositoryRoot, "Tests", "Integration.Tests", "obj", "integration-api-publish")
            : configuredDirectory;

        if (
            Directory.Exists(publishDirectory)
            && File.Exists(Path.Combine(publishDirectory, "Api.dll"))
            && File.Exists(Path.Combine(publishDirectory, "Dockerfile"))
        )
        {
            return publishDirectory;
        }

        throw new DirectoryNotFoundException(
            $"The API publish directory for integration tests was not found or is incomplete: {publishDirectory}. "
            + "Build Tests/Integration.Tests first so the runtime-only Docker image context can be generated."
        );
    }

    private static async Task<string> TryReadContainerLogs(
        IContainer container,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var logs = await container.GetLogsAsync(
                DateTime.UtcNow.AddMinutes(-10),
                DateTime.UtcNow,
                true,
                cancellationToken
            );

            return $"stdout:{Environment.NewLine}{logs.Stdout}{Environment.NewLine}stderr:{Environment.NewLine}{logs.Stderr}";
        }
        catch (Exception ex)
        {
            return $"Could not read API container logs: {ex.Message}";
        }
    }

    private static void ConfigureReadableDockerConfig(string repositoryRoot)
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_CONFIG")))
        {
            return;
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var defaultDockerConfig = Path.Combine(userProfile, ".docker", "config.json");
        if (CanReadFile(defaultDockerConfig))
        {
            return;
        }

        var localDockerConfig = Path.Combine(repositoryRoot, ".tmp", "testcontainers-docker-config");
        Directory.CreateDirectory(localDockerConfig);
        Environment.SetEnvironmentVariable("DOCKER_CONFIG", localDockerConfig);
    }

    private static bool CanReadFile(string path)
    {
        if (!File.Exists(path))
        {
            return true;
        }

        try
        {
            using var stream = File.OpenRead(path);
            return stream.CanRead;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDockerUnavailable(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var message = current.Message;
            if (
                message.Contains("Docker is either not running", StringComparison.OrdinalIgnoreCase)
                || message.Contains("DockerEndpointAuthConfig", StringComparison.OrdinalIgnoreCase)
                || message.Contains("docker_engine", StringComparison.OrdinalIgnoreCase)
                || message.Contains("permission denied", StringComparison.OrdinalIgnoreCase)
            )
            {
                return true;
            }
        }

        return false;
    }
}
