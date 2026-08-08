using DataQL.Abstractions;
using DataQL.Contracts;
using DataQL.Cosmos;
using DataQL.Cosmos.DependencyInjection;
using DataQL.Cosmos.Execution;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;

namespace DataQL.Cosmos.Tests.DependencyInjection;

public class CosmosDataQLProviderExecutorTests
{
    [Fact]
    public void Provider_ReturnsCosmos()
    {
        var executor = new CosmosDataQLProviderExecutor(new CosmosQueryExecutionEngine());
        Assert.Equal(ProviderName.Cosmos, executor.Provider);
        Assert.NotNull(executor.Capabilities);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonCosmosSession_ThrowsInvalidOperationException()
    {
        var executor = new CosmosDataQLProviderExecutor(new CosmosQueryExecutionEngine());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync<object>(
                new FakeSession(),
                new QuerySource(ProviderName.Cosmos, "Employees"),
                new QueryRequest()));

        Assert.Contains(nameof(CosmosDataQLSession), ex.Message);
        Assert.Contains(nameof(FakeSession), ex.Message);
    }

    [Fact]
    public async Task ListTablesAsync_WithNonCosmosSession_ThrowsInvalidOperationException()
    {
        var executor = new CosmosDataQLProviderExecutor(new CosmosQueryExecutionEngine());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ListTablesAsync(new FakeSession()));

        Assert.Contains(nameof(CosmosDataQLSession), ex.Message);
    }

    [Fact]
    public async Task GetTableSchemaAsync_WithNonCosmosSession_ThrowsInvalidOperationException()
    {
        var executor = new CosmosDataQLProviderExecutor(new CosmosQueryExecutionEngine());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.GetTableSchemaAsync(new FakeSession(), "Employees"));

        Assert.Contains(nameof(CosmosDataQLSession), ex.Message);
    }

    private sealed class FakeSession : IDataQLSession
    {
        public string Provider => "Fake";
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
