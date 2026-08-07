using DataQL.Contracts;
using DataQL.Validation;

namespace DataQL.Sqlite.Tests.Execution;

public class SqliteDataQLServiceDistinctE2eTests
{
    [Fact]
    public async Task ExecuteAsync_WithDistinctCity_ReturnsUniqueCities()
    {
        await using var harness = SqliteDataQLServiceE2eTestHarness.Create();

        var response = await harness.Service.ExecuteAsync<CityRow>(
            "sample",
            "Employees",
            new QueryRequest
            {
                Distinct = ["City"],
                Order = [new OrderClause { Field = "City", Direction = "asc" }],
                IncludeCount = true
            });

        Assert.Equal(["Bengaluru", "Delhi", "Pune"], response.Results.Select(static r => r.City).ToArray());
        Assert.Equal(3, response.Count);
        Assert.False(response.HasMore);
    }

    [Fact]
    public async Task ExecuteAsync_WithDistinctAndSelectSubset_StillProjectsAllDistinctFields()
    {
        await using var harness = SqliteDataQLServiceE2eTestHarness.Create();

        var response = await harness.Service.ExecuteAsync<CityDepartmentRow>(
            "sample",
            "Employees",
            new QueryRequest
            {
                Distinct = ["City", "Department"],
                Select = ["City"],
                Order =
                [
                    new OrderClause { Field = "City", Direction = "asc" },
                    new OrderClause { Field = "Department", Direction = "asc" }
                ]
            });

        Assert.Equal(4, response.Results.Count);
        Assert.Contains(response.Results, static r => r.City == "Delhi" && r.Department == "Engineering");
        Assert.Contains(response.Results, static r => r.City == "Delhi" && r.Department == "Sales");
    }

    [Fact]
    public async Task ExecuteAsync_WithOrderOutsideDistinct_ThrowsAstValidationException()
    {
        await using var harness = SqliteDataQLServiceE2eTestHarness.Create();

        var request = new QueryRequest
        {
            Distinct = ["City"],
            Order = [new OrderClause { Field = "Name", Direction = "asc" }]
        };

        var ex = await Assert.ThrowsAsync<AstValidationException>(() =>
            harness.Service.ExecuteAsync<CityRow>("sample", "Employees", request));

        Assert.Contains(ex.Errors, static e => e.Code == "Distinct.OrderNotSubset");
    }

    private sealed class CityRow
    {
        public string City { get; init; } = string.Empty;
    }

    private sealed class CityDepartmentRow
    {
        public string City { get; init; } = string.Empty;
        public string Department { get; init; } = string.Empty;
    }
}
