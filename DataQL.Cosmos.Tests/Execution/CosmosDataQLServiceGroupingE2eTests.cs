using DataQL.Contracts;
using DataQL.Cosmos.Tests.Infrastructure;

namespace DataQL.Cosmos.Tests.Execution;

[Collection(CosmosE2eCollection.Name)]
public class CosmosDataQLServiceGroupingE2eTests
{
    private readonly CosmosE2eFixture _fixture;

    public CosmosDataQLServiceGroupingE2eTests(CosmosE2eFixture fixture)
    {
        _fixture = fixture;
    }

    [CosmosAvailableFact]
    public async Task ExecuteAsync_WithGroupByAndCount_ReturnsGroupedRows()
    {
        await using var harness = CosmosDataQLServiceE2eTestHarness.Create(_fixture);

        var response = await harness.Service.ExecuteAsync<DepartmentCountRow>(
            CosmosTestEnvironment.SourceKey,
            CosmosTestEnvironment.ContainerId,
            new QueryRequest
            {
                Order = [new OrderClause { Field = "Department", Direction = "asc" }],
                Group = new GroupRequest
                {
                    GroupBy = ["Department"],
                    Metrics =
                    [
                        new GroupMetricRequest
                        {
                            Field = "*",
                            Operation = "count",
                            Alias = "Employees"
                        }
                    ]
                }
            });

        Assert.Equal(2, response.Results.Count);
        Assert.Equal("Engineering", response.Results[0].Department);
        Assert.Equal(3, response.Results[0].Employees);
        Assert.Equal("Sales", response.Results[1].Department);
        Assert.Equal(1, response.Results[1].Employees);
        Assert.False(response.HasMore);
        Assert.Null(response.ContinuationToken);
    }

    [CosmosAvailableFact]
    public async Task ExecuteAsync_WithGroupByAndLimit_IgnoresLimitAndReturnsAllGroups()
    {
        await using var harness = CosmosDataQLServiceE2eTestHarness.Create(_fixture);

        var response = await harness.Service.ExecuteAsync<DepartmentCountRow>(
            CosmosTestEnvironment.SourceKey,
            CosmosTestEnvironment.ContainerId,
            new QueryRequest
            {
                Limit = 1,
                Order = [new OrderClause { Field = "Department", Direction = "asc" }],
                Group = new GroupRequest
                {
                    GroupBy = ["Department"],
                    Metrics =
                    [
                        new GroupMetricRequest
                        {
                            Field = "*",
                            Operation = "count",
                            Alias = "Employees"
                        }
                    ]
                }
            });

        Assert.Equal(2, response.Results.Count);
        Assert.False(response.HasMore);
        Assert.Null(response.ContinuationToken);
    }

    private sealed class DepartmentCountRow
    {
        public string Department { get; init; } = string.Empty;
        public long Employees { get; init; }
    }
}
