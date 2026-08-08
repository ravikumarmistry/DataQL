using DataQL.Contracts;

namespace DataQL.Sqlite.Tests.Execution;

public class SqliteDataQLServicePagingE2eTests
{
    [Fact]
    public async Task ExecuteAsync_WithContinuationToken_ReturnsNextPage()
    {
        await using var harness = SqliteDataQLServiceE2eTestHarness.Create();

        var firstResponse = await harness.Service.ExecuteAsync<EmployeeRow>(
            "sample",
            "Employees",
            new QueryRequest
            {
                Order = [new OrderClause { Field = "Age", Direction = "desc" }],
                Limit = 2,
                IncludeCount = false
            });

        Assert.True(firstResponse.HasMore);
        Assert.NotNull(firstResponse.ContinuationToken);
        Assert.Equal("Riya", firstResponse.Results[0].Name);
        Assert.Equal("Arun", firstResponse.Results[1].Name);

        var secondResponse = await harness.Service.ExecuteAsync<EmployeeRow>(
            "sample",
            "Employees",
            new QueryRequest
            {
                Order = [new OrderClause { Field = "Age", Direction = "desc" }],
                Limit = 2,
                ContinuationToken = firstResponse.ContinuationToken,
                IncludeCount = false
            });

        Assert.False(secondResponse.HasMore);
        Assert.Null(secondResponse.ContinuationToken);
        Assert.Equal(2, secondResponse.Results.Count);
        Assert.Equal("Karan", secondResponse.Results[0].Name);
        Assert.Equal("Asha", secondResponse.Results[1].Name);
    }

    [Fact]
    public async Task ExecuteAsync_WithContinuationTokenAndChangedRequestShape_ThrowsArgumentException()
    {
        await using var harness = SqliteDataQLServiceE2eTestHarness.Create();

        var firstResponse = await harness.Service.ExecuteAsync<EmployeeRow>(
            "sample",
            "Employees",
            new QueryRequest
            {
                Order = [new OrderClause { Field = "Age", Direction = "desc" }],
                Limit = 2,
                IncludeCount = false
            });

        Assert.NotNull(firstResponse.ContinuationToken);

        var changedRequest = new QueryRequest
        {
            Where = System.Text.Json.JsonDocument.Parse("""{"Department":"Engineering"}""").RootElement.Clone(),
            Order = [new OrderClause { Field = "Age", Direction = "desc" }],
            Limit = 2,
            ContinuationToken = firstResponse.ContinuationToken,
            IncludeCount = false
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            harness.Service.ExecuteAsync<EmployeeRow>("sample", "Employees", changedRequest));

        Assert.Contains("does not match request shape", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidContinuationToken_ThrowsArgumentException()
    {
        await using var harness = SqliteDataQLServiceE2eTestHarness.Create();

        var request = new QueryRequest
        {
            Order = [new OrderClause { Field = "Age", Direction = "desc" }],
            Limit = 2,
            ContinuationToken = "garbage-token"
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            harness.Service.ExecuteAsync<EmployeeRow>("sample", "Employees", request));

        Assert.Contains("Invalid continuation token", ex.Message);
    }

    private sealed class EmployeeRow
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public int Age { get; init; }
        public string City { get; init; } = string.Empty;
    }
}
