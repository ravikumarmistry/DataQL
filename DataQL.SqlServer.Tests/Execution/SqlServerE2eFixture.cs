using DataQL.SqlServer.Tests.Infrastructure;
using Microsoft.Data.SqlClient;

namespace DataQL.SqlServer.Tests.Execution;

/// <summary>
/// Connects to the DataQL database seeded by DataQL.SqlServer.Tests/docker.
/// Tests must not mutate seeded rows.
/// </summary>
public sealed class SqlServerE2eFixture : IAsyncLifetime
{
    public string ConnectionString { get; private set; } = string.Empty;

    public string DatabaseName { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        if (!SqlServerTestEnvironment.IsAvailable)
        {
            return;
        }

        DatabaseName = SqlServerTestEnvironment.DatabaseName;
        ConnectionString = SqlServerTestEnvironment.GetDatabaseConnectionString();

        await EnsureSeededEmployeesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task EnsureSeededEmployeesAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(1)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'Employees';
            """;
        var tableCount = Convert.ToInt32(await command.ExecuteScalarAsync());
        if (tableCount == 0)
        {
            throw new InvalidOperationException(
                $"Database '{DatabaseName}' has no dbo.Employees table. "
                + "Start DataQL.SqlServer.Tests/docker so compose init can seed the database.");
        }

        await using var columnCommand = connection.CreateCommand();
        columnCommand.CommandText = """
            SELECT COUNT(1)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = 'dbo'
              AND TABLE_NAME = 'Employees'
              AND COLUMN_NAME IN ('Tags', 'Skills', 'Address', 'Projects');
            """;
        var jsonColumnCount = Convert.ToInt32(await columnCommand.ExecuteScalarAsync());
        if (jsonColumnCount < 4)
        {
            throw new InvalidOperationException(
                $"Database '{DatabaseName}'.dbo.Employees is missing JSON seed columns (Tags/Skills/Address/Projects). "
                + "Recreate the docker volume: cd DataQL.SqlServer.Tests/docker && docker compose down -v && docker compose up -d.");
        }

        await using var rowCountCommand = connection.CreateCommand();
        rowCountCommand.CommandText = "SELECT COUNT(1) FROM dbo.Employees;";
        var rowCount = Convert.ToInt32(await rowCountCommand.ExecuteScalarAsync());
        if (rowCount == 0)
        {
            throw new InvalidOperationException(
                $"Database '{DatabaseName}'.dbo.Employees is empty. "
                + "Start DataQL.SqlServer.Tests/docker so compose init can seed the database.");
        }
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerE2eCollection : ICollectionFixture<SqlServerE2eFixture>
{
    public const string Name = "SqlServerE2e";
}
