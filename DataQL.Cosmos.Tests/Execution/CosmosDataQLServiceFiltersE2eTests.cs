using DataQL.Contracts;
using DataQL.Cosmos.Tests.Infrastructure;

namespace DataQL.Cosmos.Tests.Execution;

[Collection(CosmosE2eCollection.Name)]
public class CosmosDataQLServiceFiltersE2eTests
{
    private readonly CosmosE2eFixture _fixture;

    public CosmosDataQLServiceFiltersE2eTests(CosmosE2eFixture fixture)
    {
        _fixture = fixture;
    }

    [CosmosAvailableFact]
    public async Task ExecuteAsync_WithLimit_ReturnsPageAndMetaRequestCharge()
    {
        await using var harness = CosmosDataQLServiceE2eTestHarness.Create(_fixture);

        var response = await harness.Service.ExecuteAsync<EmployeeRow>(
            CosmosTestEnvironment.SourceKey,
            CosmosTestEnvironment.ContainerId,
            new QueryRequest
            {
                Where = QueryFilterBuilder.Field("Age").Gte(21).ToJsonElement(),
                Order = [new OrderClause { Field = "Age", Direction = "desc" }],
                Limit = 2
            });

        Assert.Equal(2, response.Results.Count);
        Assert.Equal("Riya", response.Results[0].Name);
        Assert.Equal("Arun", response.Results[1].Name);
        Assert.True(response.HasMore);
        Assert.NotNull(response.ContinuationToken);
        Assert.Null(response.Count);
        Assert.NotNull(response.Meta);
        Assert.Equal("Cosmos", response.Meta.Provider);
        Assert.True(response.Meta.ExecutionTimeMs >= 0);
        Assert.NotNull(response.Meta.Extensions);
        Assert.True(response.Meta.Extensions.ContainsKey("requestCharge"));
    }

    [CosmosAvailableFact]
    public async Task ExecuteAsync_Eq_ReturnsMatchingRow()
    {
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("Name").Eq("Asha"));
        Assert.Equal(["Asha"], names);
    }

    [CosmosAvailableFact]
    public async Task ExecuteAsync_And_CombinesPredicates()
    {
        var filter = QueryFilterBuilder.And(
            QueryFilterBuilder.Field("Department").Eq("Engineering"),
            QueryFilterBuilder.Field("IsActive").Eq(true));

        var names = await QueryNamesAsync(filter);
        Assert.Equal(["Arun", "Asha"], names);
    }

    [CosmosAvailableFact]
    public async Task ExecuteAsync_WithSelect_ProjectsRequestedFields()
    {
        await using var harness = CosmosDataQLServiceE2eTestHarness.Create(_fixture);

        var response = await harness.Service.ExecuteAsync<NameAgeRow>(
            CosmosTestEnvironment.SourceKey,
            CosmosTestEnvironment.ContainerId,
            new QueryRequest
            {
                Where = QueryFilterBuilder.Field("Name").Eq("Asha").ToJsonElement(),
                Order = [new OrderClause { Field = "id", Direction = "asc" }],
                Select = ["Name", "Age"]
            });

        Assert.Single(response.Results);
        Assert.Equal("Asha", response.Results[0].Name);
        Assert.Equal(19, response.Results[0].Age);
    }

    private async Task<IReadOnlyList<string>> QueryNamesAsync(QueryFilter filter)
    {
        await using var harness = CosmosDataQLServiceE2eTestHarness.Create(_fixture);

        var response = await harness.Service.ExecuteAsync<EmployeeRow>(
            CosmosTestEnvironment.SourceKey,
            CosmosTestEnvironment.ContainerId,
            new QueryRequest
            {
                Where = filter.ToJsonElement(),
                Order = [new OrderClause { Field = "Name", Direction = "asc" }]
            });

        return response.Results.Select(static r => r.Name).ToArray();
    }

    private sealed class EmployeeRow
    {
        public string id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public int Age { get; init; }
        public string City { get; init; } = string.Empty;
        public string Department { get; init; } = string.Empty;
        public bool IsActive { get; init; }
        public string? Notes { get; init; }
    }

    private sealed class NameAgeRow
    {
        public string Name { get; init; } = string.Empty;
        public int Age { get; init; }
    }
}
