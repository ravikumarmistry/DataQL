using DataQL.Abstractions;
using DataQL.Contracts;
using DataQL.Sqlite.DependencyInjection;
using DataQL.Sqlite.Execution;

namespace DataQL.Sqlite.Tests.DependencyInjection;

public class SqliteDataQLProviderExecutorTests
{
    [Fact]
    public void Provider_ReturnsSqlite()
    {
        var executor = new SqliteDataQLProviderExecutor(new SqliteQueryExecutionEngine());
        Assert.Equal(ProviderName.Sqlite, executor.Provider);
        Assert.NotNull(executor.Capabilities);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonAdoSession_ThrowsInvalidOperationException()
    {
        var executor = new SqliteDataQLProviderExecutor(new SqliteQueryExecutionEngine());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync<object>(
                new FakeSession(),
                new QuerySource(ProviderName.Sqlite, "Employees"),
                new QueryRequest()));

        Assert.Contains(nameof(AdoDataQLSession), ex.Message);
        Assert.Contains(nameof(FakeSession), ex.Message);
    }

    [Fact]
    public async Task ListTablesAsync_WithNonAdoSession_ThrowsInvalidOperationException()
    {
        var executor = new SqliteDataQLProviderExecutor(new SqliteQueryExecutionEngine());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ListTablesAsync(new FakeSession()));

        Assert.Contains(nameof(AdoDataQLSession), ex.Message);
    }

    [Fact]
    public async Task GetTableSchemaAsync_WithNonAdoSession_ThrowsInvalidOperationException()
    {
        var executor = new SqliteDataQLProviderExecutor(new SqliteQueryExecutionEngine());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.GetTableSchemaAsync(new FakeSession(), "Employees"));

        Assert.Contains(nameof(AdoDataQLSession), ex.Message);
    }

    private sealed class FakeSession : IDataQLSession
    {
        public string Provider => "Fake";
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
