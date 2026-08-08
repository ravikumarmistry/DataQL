using DataQL;
using DataQL.AspNetCore;
using DataQL.AspNetCore.OpenApi;
using DataQL.Sqlite.DependencyInjection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DataQL.Sqlite.Tests.Execution;

internal sealed class SqliteDataQLServiceE2eTestHarness : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly SqliteConnection _keeperConnection;

    private SqliteDataQLServiceE2eTestHarness(
        IDataQLService service,
        IDataQLMetaService metaService,
        IDataQLOpenApiDocumentProvider openApi,
        ServiceProvider provider,
        SqliteConnection keeperConnection)
    {
        Service = service;
        MetaService = metaService;
        OpenApi = openApi;
        _provider = provider;
        _keeperConnection = keeperConnection;
    }

    public IDataQLService Service { get; }

    public IDataQLMetaService MetaService { get; }

    public IDataQLOpenApiDocumentProvider OpenApi { get; }

    public static SqliteDataQLServiceE2eTestHarness Create(Action<DataQLOptions>? configure = null)
    {
        var sharedName = "dataql-e2e-" + Guid.NewGuid().ToString("N");
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = sharedName,
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        var keeper = new SqliteConnection(connectionString);
        keeper.Open();
        Seed(keeper);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddDataQL(options =>
        {
            options.AddSqliteSource("sample", _ => new SqliteConnection(connectionString));
            configure?.Invoke(options);
        });
        services.AddDataQLOpenApi();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IDataQLService>();
        var metaService = provider.GetRequiredService<IDataQLMetaService>();
        var openApi = provider.GetRequiredService<IDataQLOpenApiDocumentProvider>();
        return new SqliteDataQLServiceE2eTestHarness(service, metaService, openApi, provider, keeper);
    }

    public async ValueTask DisposeAsync()
    {
        await _keeperConnection.DisposeAsync();
        await _provider.DisposeAsync();
    }

    // Keep aligned with testdata/Employees.json / provider docker init seed rows.
    private static void Seed(SqliteConnection connection)
    {
        using var create = connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE Employees (
              Id INTEGER PRIMARY KEY,
              Name TEXT NOT NULL,
              Age INTEGER NOT NULL,
              City TEXT NOT NULL,
              Department TEXT NOT NULL,
              IsActive INTEGER NOT NULL,
              Notes TEXT NULL,
              CreatedAt TEXT NOT NULL
            );
            INSERT INTO Employees (Id, Name, Age, City, Department, IsActive, Notes, CreatedAt) VALUES
            (1, 'Asha', 19, 'Delhi', 'Engineering', 1, 'junior', '2025-01-10T10:00:00Z'),
            (2, 'Arun', 24, 'Bengaluru', 'Engineering', 1, NULL, '2025-01-11T10:00:00Z'),
            (3, 'Riya', 31, 'Delhi', 'Sales', 1, 'lead', '2025-01-12T10:00:00Z'),
            (4, 'Karan', 22, 'Pune', 'Engineering', 0, NULL, '2025-01-13T10:00:00Z');
            """;
        create.ExecuteNonQuery();
    }
}
