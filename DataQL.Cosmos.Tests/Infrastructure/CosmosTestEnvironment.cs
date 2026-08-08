using System.Net.Http;
using Microsoft.Azure.Cosmos;

namespace DataQL.Cosmos.Tests.Infrastructure;

internal static class CosmosTestEnvironment
{
    public const string DefaultSourceKey = "p_cosmos";
    public const string DefaultEndpoint = "https://localhost:8081";
    public const string DefaultKey =
        "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
    public const string DefaultDatabase = "DataQL";
    public const string DefaultContainer = "Employees";

    private static readonly Lazy<IReadOnlyDictionary<string, string>> DockerEnv = new(LoadDockerEnv);
    private static readonly Lazy<bool> Available = new(ProbeAvailability);

    public static bool IsAvailable => Available.Value;

    public static string SourceKey =>
        GetSetting("DATAQL_SOURCE_KEY")
        ?? DefaultSourceKey;

    public static string Endpoint =>
        Environment.GetEnvironmentVariable("DATAQL_COSMOS_ENDPOINT")
        ?? GetSetting("COSMOS_ENDPOINT")
        ?? DefaultEndpoint;

    public static string Key =>
        Environment.GetEnvironmentVariable("DATAQL_COSMOS_KEY")
        ?? GetSetting("COSMOS_KEY")
        ?? DefaultKey;

    public static string DatabaseId =>
        Environment.GetEnvironmentVariable("DATAQL_COSMOS_DATABASE")
        ?? GetSetting("COSMOS_DATABASE")
        ?? DefaultDatabase;

    public static string ContainerId =>
        GetSetting("COSMOS_CONTAINER")
        ?? DefaultContainer;

    public static CosmosClient CreateClient()
    {
        return new CosmosClient(Endpoint, Key, new CosmosClientOptions
        {
            ConnectionMode = ConnectionMode.Gateway,
            LimitToEndpoint = true,
            HttpClientFactory = () =>
            {
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };
                return new HttpClient(handler);
            }
        });
    }

    private static string? GetSetting(string key)
    {
        var fromProcess = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(fromProcess))
        {
            return fromProcess;
        }

        return DockerEnv.Value.TryGetValue(key, out var value) ? value : null;
    }

    private static IReadOnlyDictionary<string, string> LoadDockerEnv()
    {
        var path = FindDockerEnvFile();
        if (path is null)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (value.Length >= 2
                && ((value.StartsWith('"') && value.EndsWith('"'))
                    || (value.StartsWith('\'') && value.EndsWith('\''))))
            {
                value = value[1..^1];
            }

            values[key] = value;
        }

        return values;
    }

    private static string? FindDockerEnvFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "DataQL.Cosmos.Tests.csproj")))
            {
                var env = Path.Combine(dir.FullName, "docker", ".env");
                if (File.Exists(env))
                {
                    return env;
                }

                var example = Path.Combine(dir.FullName, "docker", ".env.example");
                if (File.Exists(example))
                {
                    return example;
                }
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static bool ProbeAvailability()
    {
        try
        {
            using var client = CreateClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            client.ReadAccountAsync().Wait(cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
