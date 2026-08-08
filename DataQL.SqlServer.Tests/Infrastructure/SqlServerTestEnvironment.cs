using Microsoft.Data.SqlClient;

namespace DataQL.SqlServer.Tests.Infrastructure;

/// <summary>
/// Local SQL Server settings for DataQL.SqlServer tests (source key <c>p_sqlserver</c>).
/// Credentials come from this project's <c>docker/.env</c> (or <c>.env.example</c>).
/// </summary>
internal static class SqlServerTestEnvironment
{
    public const string ConnectionEnvironmentVariable = "DATAQL_SQLSERVER_CONNECTION";
    public const string DatabaseEnvironmentVariable = "DATAQL_SQLSERVER_DATABASE";
    public const string DefaultSourceKey = "p_sqlserver";

    private const string ProjectFileName = "DataQL.SqlServer.Tests.csproj";

    private static readonly Lazy<IReadOnlyDictionary<string, string>> DockerEnv = new(LoadDockerEnv);
    private static readonly Lazy<bool> Available = new(ProbeAvailability);

    public static bool IsAvailable => Available.Value;

    public static string SourceKey =>
        GetSetting("DATAQL_SOURCE_KEY")
        ?? DefaultSourceKey;

    public static string DatabaseName =>
        Environment.GetEnvironmentVariable(DatabaseEnvironmentVariable)
        ?? GetSetting("MSSQL_DATABASE")
        ?? "DataQL";

    public static string GetServerConnectionString() =>
        ToCatalogConnectionString(catalog: "master", connectTimeout: 3, pooling: true);

    public static string GetDatabaseConnectionString() =>
        ToCatalogConnectionString(catalog: DatabaseName, connectTimeout: 15, pooling: false);

    private static string ToCatalogConnectionString(string catalog, int connectTimeout, bool pooling)
    {
        var configured = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var builder = new SqlConnectionStringBuilder(configured)
            {
                InitialCatalog = catalog
            };
            if (builder.ConnectTimeout <= 0)
            {
                builder.ConnectTimeout = connectTimeout;
            }

            return builder.ConnectionString;
        }

        var host = GetSetting("MSSQL_HOST") ?? "localhost";
        var port = GetSetting("MSSQL_PORT") ?? "1433";
        var user = GetSetting("MSSQL_USER") ?? "sa";
        var password = GetSetting("MSSQL_SA_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "SQL Server password not found. Copy DataQL.SqlServer.Tests/docker/.env.example to .env "
                + $"or set {ConnectionEnvironmentVariable}.");
        }

        return new SqlConnectionStringBuilder
        {
            DataSource = $"{host},{port}",
            UserID = user,
            Password = password,
            TrustServerCertificate = true,
            InitialCatalog = catalog,
            ConnectTimeout = connectTimeout,
            Pooling = pooling
        }.ConnectionString;
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
            if (File.Exists(Path.Combine(dir.FullName, ProjectFileName)))
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
            using var connection = new SqlConnection(GetServerConnectionString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.ExecuteScalar();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
