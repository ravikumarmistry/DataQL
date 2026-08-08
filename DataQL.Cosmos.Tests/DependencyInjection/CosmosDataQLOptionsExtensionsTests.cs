using DataQL;
using DataQL.Cosmos.DependencyInjection;
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
}
