using System.Data;
using System.Diagnostics.CodeAnalysis;
using DataQL.Sqlite.Metadata;
using Microsoft.Data.Sqlite;

namespace DataQL.Sqlite.Tests.Metadata;

public class SqliteMetadataProviderTests
{
    [Fact]
    public async Task GetTableSchemaAsync_WithMissingTable_ThrowsInvalidOperationException()
    {
        await using var connection = CreateSeededConnection();
        var provider = new SqliteMetadataProvider();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetTableSchemaAsync(connection, "MissingTable"));

        Assert.Contains("MissingTable", ex.Message);
    }

    [Fact]
    public async Task GetTableSchemaAsync_WithEmptyTableName_ThrowsArgumentException()
    {
        await using var connection = CreateSeededConnection();
        var provider = new SqliteMetadataProvider();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            provider.GetTableSchemaAsync(connection, "  "));
    }

    [Fact]
    public async Task GetTableSchemaAsync_WithNonDbConnection_ThrowsNotSupportedException()
    {
        var provider = new SqliteMetadataProvider();

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() =>
            provider.GetTableSchemaAsync(new FakeNonDbConnection(), "Employees"));

        Assert.Contains("DbConnection", ex.Message);
    }

    private static SqliteConnection CreateSeededConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE Employees (
              Id INTEGER PRIMARY KEY,
              Name TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
        return connection;
    }

    private sealed class FakeNonDbConnection : IDbConnection
    {
        [AllowNull]
        public string ConnectionString { get; set; } = string.Empty;
        public int ConnectionTimeout => 0;
        public string Database => "Fake";
        public ConnectionState State => ConnectionState.Open;

        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public IDbCommand CreateCommand() => throw new NotSupportedException();
        public void Open() { }
        public void Dispose() { }
    }
}
