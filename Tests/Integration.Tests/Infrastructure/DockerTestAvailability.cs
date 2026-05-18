using System.Diagnostics;

namespace Integration.Tests.Infrastructure;

public static class DockerTestAvailability
{
    private static readonly Lazy<bool> DockerAvailable = new(CheckDockerAvailable);

    public static bool IsDockerAvailable => DockerAvailable.Value;

    private static bool CheckDockerAvailable()
    {
        try
        {
            ConfigureReadableDockerConfig();

            using var process = Process.Start(
                new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "info --format {{.ServerVersion}}",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            );

            if (process is null)
            {
                return false;
            }

            if (!process.WaitForExit(TimeSpan.FromSeconds(10)))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best effort only; the check will report Docker as unavailable.
                }

                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void ConfigureReadableDockerConfig()
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

        var localDockerConfig = Path.Combine(FindRepositoryRoot(), ".tmp", "testcontainers-docker-config");
        Directory.CreateDirectory(localDockerConfig);
        Environment.SetEnvironmentVariable("DOCKER_CONFIG", localDockerConfig);
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

        return Directory.GetCurrentDirectory();
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
}
