using DataQL.Abstractions;
using DataQL.Contracts;
using DataQL.SqlServer.DependencyInjection;
using DataQL.SqlServer.Execution;

namespace DataQL.SqlServer.Tests.DependencyInjection;

public class SqlServerDataQLProviderExecutorTests
{
    [Fact]
    public void Provider_ReturnsSqlServer()
    {
        var executor = new SqlServerDataQLProviderExecutor(new SqlServerQueryExecutionEngine());
        Assert.Equal(ProviderName.SqlServer, executor.Provider);
        Assert.NotNull(executor.Capabilities);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonAdoSession_ThrowsInvalidOperationException()
    {
        var executor = new SqlServerDataQLProviderExecutor(new SqlServerQueryExecutionEngine());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync<object>(
                new FakeSession(),
                new QuerySource(ProviderName.SqlServer, "Employees"),
                new QueryRequest()));

        Assert.Contains(nameof(AdoDataQLSession), ex.Message);
        Assert.Contains(nameof(FakeSession), ex.Message);
    }

    [Fact]
    public async Task ListTablesAsync_WithNonAdoSession_ThrowsInvalidOperationException()
    {
        var executor = new SqlServerDataQLProviderExecutor(new SqlServerQueryExecutionEngine());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ListTablesAsync(new FakeSession()));

        Assert.Contains(nameof(AdoDataQLSession), ex.Message);
    }

    [Fact]
    public async Task GetTableSchemaAsync_WithNonAdoSession_ThrowsInvalidOperationException()
    {
        var executor = new SqlServerDataQLProviderExecutor(new SqlServerQueryExecutionEngine());

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
