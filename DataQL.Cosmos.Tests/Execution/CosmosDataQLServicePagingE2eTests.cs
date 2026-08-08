using DataQL.Contracts;
using DataQL.Cosmos.Tests.Infrastructure;

namespace DataQL.Cosmos.Tests.Execution;

[Collection(CosmosE2eCollection.Name)]
public class CosmosDataQLServicePagingE2eTests
{
    private readonly CosmosE2eFixture _fixture;

    public CosmosDataQLServicePagingE2eTests(CosmosE2eFixture fixture)
    {
        _fixture = fixture;
    }

    [CosmosAvailableFact]
    public async Task ExecuteAsync_WithContinuationToken_ReturnsNextPage()
    {
        await using var harness = CosmosDataQLServiceE2eTestHarness.Create(_fixture);

        var firstResponse = await harness.Service.ExecuteAsync<EmployeeRow>(
            CosmosTestEnvironment.SourceKey,
            CosmosTestEnvironment.ContainerId,
            new QueryRequest
            {
                Order = [new OrderClause { Field = "Age", Direction = "desc" }],
                Limit = 2
            });

        Assert.True(firstResponse.HasMore);
        Assert.NotNull(firstResponse.ContinuationToken);
        Assert.Equal(2, firstResponse.Results.Count);

        var secondResponse = await harness.Service.ExecuteAsync<EmployeeRow>(
            CosmosTestEnvironment.SourceKey,
            CosmosTestEnvironment.ContainerId,
            new QueryRequest
            {
                Order = [new OrderClause { Field = "Age", Direction = "desc" }],
                Limit = 2,
                ContinuationToken = firstResponse.ContinuationToken
            });

        Assert.Equal(2, secondResponse.Results.Count);
        Assert.DoesNotContain(
            secondResponse.Results.Select(static r => r.id),
            id => firstResponse.Results.Any(r => r.id == id));
    }

    [CosmosAvailableFact]
    public async Task ExecuteAsync_WithContinuationTokenAndChangedRequestShape_ThrowsArgumentException()
    {
        await using var harness = CosmosDataQLServiceE2eTestHarness.Create(_fixture);

        var firstResponse = await harness.Service.ExecuteAsync<EmployeeRow>(
            CosmosTestEnvironment.SourceKey,
            CosmosTestEnvironment.ContainerId,
            new QueryRequest
            {
                Order = [new OrderClause { Field = "Age", Direction = "desc" }],
                Limit = 2
            });

        Assert.NotNull(firstResponse.ContinuationToken);

        var changedRequest = new QueryRequest
        {
            Where = System.Text.Json.JsonDocument.Parse("""{"Department":"Engineering"}""").RootElement.Clone(),
            Order = [new OrderClause { Field = "Age", Direction = "desc" }],
            Limit = 2,
            ContinuationToken = firstResponse.ContinuationToken
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            harness.Service.ExecuteAsync<EmployeeRow>(
                CosmosTestEnvironment.SourceKey,
                CosmosTestEnvironment.ContainerId,
                changedRequest));

        Assert.Contains("does not match request shape", ex.Message);
    }

    private sealed class EmployeeRow
    {
        public string id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public int Age { get; init; }
    }
}
