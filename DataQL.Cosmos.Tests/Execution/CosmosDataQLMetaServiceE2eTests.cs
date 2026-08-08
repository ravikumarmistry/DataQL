using DataQL.Abstractions;
using DataQL.Cosmos.Tests.Infrastructure;

namespace DataQL.Cosmos.Tests.Execution;

[Collection(CosmosE2eCollection.Name)]
public class CosmosDataQLMetaServiceE2eTests
{
    private readonly CosmosE2eFixture _fixture;

    public CosmosDataQLMetaServiceE2eTests(CosmosE2eFixture fixture)
    {
        _fixture = fixture;
    }

    [CosmosAvailableFact]
    public async Task ListSourcesAsync_ReturnsRegisteredSources()
    {
        await using var harness = CosmosDataQLServiceE2eTestHarness.Create(_fixture);

        var sources = await harness.MetaService.ListSourcesAsync();

        Assert.Single(sources);
        Assert.Equal(CosmosTestEnvironment.SourceKey, sources[0].Key);
        Assert.Equal(ProviderName.Cosmos, sources[0].Provider);
    }

    [CosmosAvailableFact]
    public async Task ListTablesAsync_ReturnsEmployeesContainer()
    {
        await using var harness = CosmosDataQLServiceE2eTestHarness.Create(_fixture);

        var tables = await harness.MetaService.ListTablesAsync(CosmosTestEnvironment.SourceKey);

        Assert.Contains(tables, t => t.Name == CosmosTestEnvironment.ContainerId);
    }

    [CosmosAvailableFact]
    public async Task GetTableSchemaAsync_ReturnsObjectSchema()
    {
        await using var harness = CosmosDataQLServiceE2eTestHarness.Create(_fixture);

        var schema = await harness.MetaService.GetTableSchemaAsync(
            CosmosTestEnvironment.SourceKey,
            CosmosTestEnvironment.ContainerId);

        Assert.Equal(ProviderName.Cosmos, schema.Provider);
        Assert.Equal("object", schema.Schema.GetProperty("type").GetString());
    }
}
