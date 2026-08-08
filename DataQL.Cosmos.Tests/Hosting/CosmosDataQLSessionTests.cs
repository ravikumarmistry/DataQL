using DataQL.Cosmos;
using Microsoft.Azure.Cosmos;

namespace DataQL.Cosmos.Tests;

public class CosmosDataQLSessionTests
{
    [Fact]
    public void Ctor_WithNullClient_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new CosmosDataQLSession(null!, "db"));
    }

    [Fact]
    public void Ctor_WithEmptyDatabaseId_ThrowsArgumentException()
    {
        using var client = CreateClient();
        var ex = Assert.Throws<ArgumentException>(() => new CosmosDataQLSession(client, "  "));
        Assert.Contains("Database id", ex.Message);
    }

    [Fact]
    public async Task Provider_ReturnsCosmos()
    {
        using var client = CreateClient();
        await using var session = new CosmosDataQLSession(client, "DataQL");
        Assert.Equal("Cosmos", session.Provider);
        Assert.Equal("DataQL", session.DatabaseId);
    }

    [Fact]
    public async Task GetContainer_WithEmptyId_ThrowsArgumentException()
    {
        using var client = CreateClient();
        await using var session = new CosmosDataQLSession(client, "DataQL");
        var ex = Assert.Throws<ArgumentException>(() => session.GetContainer("  "));
        Assert.Contains("Container id", ex.Message);
    }

    [Fact]
    public async Task GetContainer_WithValidId_ReturnsContainer()
    {
        using var client = CreateClient();
        await using var session = new CosmosDataQLSession(client, "DataQL");
        var container = session.GetContainer("Employees");
        Assert.NotNull(container);
    }

    [Fact]
    public async Task DisposeAsync_WhenNotOwnsClient_DoesNotThrow()
    {
        using var client = CreateClient();
        var session = new CosmosDataQLSession(client, "DataQL", ownsClient: false);
        await session.DisposeAsync();
        await session.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_WhenOwnsClient_DisposesClient()
    {
        var client = CreateClient();
        var session = new CosmosDataQLSession(client, "DataQL", ownsClient: true);
        await session.DisposeAsync();
        await session.DisposeAsync();
    }

    private static CosmosClient CreateClient() =>
        new("https://localhost:8081/", Convert.ToBase64String(new byte[64]), new CosmosClientOptions
        {
            ConnectionMode = ConnectionMode.Gateway,
            LimitToEndpoint = true
        });
}
