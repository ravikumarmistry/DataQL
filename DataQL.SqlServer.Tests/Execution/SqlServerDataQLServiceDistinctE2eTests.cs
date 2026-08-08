using DataQL.Contracts;
using DataQL.SqlServer.Tests.Infrastructure;
using DataQL.Validation;

namespace DataQL.SqlServer.Tests.Execution;

[Collection(SqlServerE2eCollection.Name)]
public class SqlServerDataQLServiceDistinctE2eTests
{
    private readonly SqlServerE2eFixture _fixture;

    public SqlServerDataQLServiceDistinctE2eTests(SqlServerE2eFixture fixture)
    {
        _fixture = fixture;
    }

    [SqlServerAvailableFact]
    public async Task ExecuteAsync_WithDistinctCity_ReturnsUniqueCities()
    {
        await using var harness = SqlServerDataQLServiceE2eTestHarness.Create(_fixture);

        var response = await harness.Service.ExecuteAsync<CityRow>(
            SqlServerTestEnvironment.SourceKey,
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

    [SqlServerAvailableFact]
    public async Task ExecuteAsync_WithDistinctAndSelectSubset_StillProjectsAllDistinctFields()
    {
        await using var harness = SqlServerDataQLServiceE2eTestHarness.Create(_fixture);

        var response = await harness.Service.ExecuteAsync<CityDepartmentRow>(
            SqlServerTestEnvironment.SourceKey,
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

    [SqlServerAvailableFact]
    public async Task ExecuteAsync_WithOrderOutsideDistinct_ThrowsAstValidationException()
    {
        await using var harness = SqlServerDataQLServiceE2eTestHarness.Create(_fixture);

        var request = new QueryRequest
        {
            Distinct = ["City"],
            Order = [new OrderClause { Field = "Name", Direction = "asc" }]
        };

        var ex = await Assert.ThrowsAsync<AstValidationException>(() =>
            harness.Service.ExecuteAsync<CityRow>(SqlServerTestEnvironment.SourceKey, "Employees", request));

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
