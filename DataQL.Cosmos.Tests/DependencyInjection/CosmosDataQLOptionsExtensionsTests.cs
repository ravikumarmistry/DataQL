using DataQL;
using DataQL.Abstractions;
using DataQL.Cosmos.DependencyInjection;
using DataQL.Cosmos.Execution;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;

namespace DataQL.Cosmos.Tests.DependencyInjection;

public class CosmosDataQLOptionsExtensionsTests
{
    [Fact]
    public void AddCosmosSource_WithNullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CosmosDataQLOptionsExtensions.AddCosmosSource(null!, "sample", _ => null!, "db"));
    }

    [Fact]
    public void AddCosmosSource_WithEmptySourceKey_ThrowsArgumentException()
    {
        var options = new DataQLOptions(new ServiceCollection());
        Assert.Throws<ArgumentException>(() =>
            options.AddCosmosSource("  ", _ => null!, "db"));
    }

    [Fact]
    public void AddCosmosSource_WithNullClientFactory_ThrowsArgumentNullException()
    {
        var options = new DataQLOptions(new ServiceCollection());
        Assert.Throws<ArgumentNullException>(() =>
            options.AddCosmosSource("sample", (Func<IServiceProvider, CosmosClient>)null!, "db"));
    }

    [Fact]
    public void AddCosmosSource_WithEmptyDatabaseId_ThrowsArgumentException()
    {
        var options = new DataQLOptions(new ServiceCollection());
        Assert.Throws<ArgumentException>(() =>
            options.AddCosmosSource("sample", _ => null!, "  "));
    }

    [Fact]
    public void AddCosmosSource_EndpointOverload_WithEmptyEndpoint_ThrowsArgumentException()
    {
        var options = new DataQLOptions(new ServiceCollection());
        Assert.Throws<ArgumentException>(() =>
            options.AddCosmosSource("sample", "  ", "key", "db"));
    }

    [Fact]
    public void AddCosmosSource_EndpointOverload_WithEmptyKey_ThrowsArgumentException()
    {
        var options = new DataQLOptions(new ServiceCollection());
        Assert.Throws<ArgumentException>(() =>
            options.AddCosmosSource("sample", "https://localhost:8081/", "  ", "db"));
    }

    [Fact]
    public void AddDataQLCosmos_RegistersEngineAndProviderExecutor()
    {
        var services = new ServiceCollection();
        services.AddDataQLCosmos();

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<CosmosQueryExecutionEngine>());
        Assert.Contains(
            provider.GetServices<IDataQLProviderExecutor>(),
            e => e is CosmosDataQLProviderExecutor);
    }

    [Fact]
    public void AddCosmosSource_RegistersEngineAndProviderExecutor()
    {
        var services = new ServiceCollection();
        services.AddDataQL(options =>
            options.AddCosmosSource("sample", _ => null!, "db"));

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<CosmosQueryExecutionEngine>());
        Assert.Contains(
            provider.GetServices<IDataQLProviderExecutor>(),
            e => e is CosmosDataQLProviderExecutor);
    }
}
