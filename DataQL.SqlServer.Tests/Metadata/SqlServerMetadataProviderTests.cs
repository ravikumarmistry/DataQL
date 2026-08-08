using System.Data;
using System.Diagnostics.CodeAnalysis;
using DataQL.SqlServer.Metadata;

namespace DataQL.SqlServer.Tests.Metadata;

public class SqlServerMetadataProviderTests
{
    [Fact]
    public async Task GetTableSchemaAsync_WithEmptyTableName_ThrowsArgumentException()
    {
        var provider = new SqlServerMetadataProvider();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            provider.GetTableSchemaAsync(new FakeNonDbConnection(), "  "));
    }

    [Fact]
    public async Task GetTableSchemaAsync_WithNonDbConnection_ThrowsNotSupportedException()
    {
        var provider = new SqlServerMetadataProvider();

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() =>
            provider.GetTableSchemaAsync(new FakeNonDbConnection(), "Employees"));

        Assert.Contains("DbConnection", ex.Message);
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
