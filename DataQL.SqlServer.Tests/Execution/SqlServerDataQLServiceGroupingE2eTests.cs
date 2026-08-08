using DataQL.Contracts;
using DataQL.SqlServer.Tests.Infrastructure;
using DataQL.Validation;

namespace DataQL.SqlServer.Tests.Execution;

[Collection(SqlServerE2eCollection.Name)]
public class SqlServerDataQLServiceGroupingE2eTests
{
    private readonly SqlServerE2eFixture _fixture;

    public SqlServerDataQLServiceGroupingE2eTests(SqlServerE2eFixture fixture)
    {
        _fixture = fixture;
    }

    [SqlServerAvailableFact]
    public async Task ExecuteAsync_WithGroupByAndCount_ReturnsGroupedRows()
    {
        await using var harness = SqlServerDataQLServiceE2eTestHarness.Create(_fixture);

        var response = await harness.Service.ExecuteAsync<DepartmentCountRow>(
            SqlServerTestEnvironment.SourceKey,
            "Employees",
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
                            Alias = "employees"
                        }
                    ]
                }
            });

        Assert.Equal(2, response.Results.Count);
        Assert.Equal("Engineering", response.Results[0].Department);
        Assert.Equal(3, response.Results[0].Employees);
        Assert.Equal("Sales", response.Results[1].Department);
        Assert.Equal(1, response.Results[1].Employees);
    }

    [SqlServerAvailableFact]
    public async Task ExecuteAsync_WithGroupByAndSum_ReturnsAgeTotals()
    {
        await using var harness = SqlServerDataQLServiceE2eTestHarness.Create(_fixture);

        var response = await harness.Service.ExecuteAsync<DepartmentSumRow>(
            SqlServerTestEnvironment.SourceKey,
            "Employees",
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
                            Field = "Age",
                            Operation = "sum",
                            Alias = "totalAge"
                        }
                    ]
                }
            });

        Assert.Equal(2, response.Results.Count);
        Assert.Equal("Engineering", response.Results[0].Department);
        Assert.Equal(65, response.Results[0].TotalAge); // 19 + 24 + 22
        Assert.Equal("Sales", response.Results[1].Department);
        Assert.Equal(31, response.Results[1].TotalAge);
    }

    [SqlServerAvailableFact]
    public async Task ExecuteAsync_WithGroupByAndAvg_ReturnsAverageAge()
    {
        await using var harness = SqlServerDataQLServiceE2eTestHarness.Create(_fixture);

        var response = await harness.Service.ExecuteAsync<DepartmentAvgRow>(
            SqlServerTestEnvironment.SourceKey,
            "Employees",
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
                            Field = "Age",
                            Operation = "avg",
                            Alias = "averageAge"
                        }
                    ]
                }
            });

        Assert.Equal(2, response.Results.Count);
        Assert.Equal("Engineering", response.Results[0].Department);
        Assert.Equal(65.0 / 3.0, response.Results[0].AverageAge, precision: 5);
        Assert.Equal("Sales", response.Results[1].Department);
        Assert.Equal(31.0, response.Results[1].AverageAge, precision: 5);
    }

    [SqlServerAvailableFact]
    public async Task ExecuteAsync_WithGroupByAndMin_ReturnsYoungestAge()
    {
        await using var harness = SqlServerDataQLServiceE2eTestHarness.Create(_fixture);

        var response = await harness.Service.ExecuteAsync<DepartmentMinRow>(
            SqlServerTestEnvironment.SourceKey,
            "Employees",
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
                            Field = "Age",
                            Operation = "min",
                            Alias = "minAge"
                        }
                    ]
                }
            });

        Assert.Equal(2, response.Results.Count);
        Assert.Equal("Engineering", response.Results[0].Department);
        Assert.Equal(19, response.Results[0].MinAge);
        Assert.Equal("Sales", response.Results[1].Department);
        Assert.Equal(31, response.Results[1].MinAge);
    }

    [SqlServerAvailableFact]
    public async Task ExecuteAsync_WithGroupByAndMax_ReturnsOldestAge()
    {
        await using var harness = SqlServerDataQLServiceE2eTestHarness.Create(_fixture);

        var response = await harness.Service.ExecuteAsync<DepartmentMaxRow>(
            SqlServerTestEnvironment.SourceKey,
            "Employees",
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
                            Field = "Age",
                            Operation = "max",
                            Alias = "maxAge"
                        }
                    ]
                }
            });

        Assert.Equal(2, response.Results.Count);
        Assert.Equal("Engineering", response.Results[0].Department);
        Assert.Equal(24, response.Results[0].MaxAge);
        Assert.Equal("Sales", response.Results[1].Department);
        Assert.Equal(31, response.Results[1].MaxAge);
    }

    [SqlServerAvailableFact]
    public async Task ExecuteAsync_WithMultipleGroupMetrics_ReturnsAllAggregates()
    {
        await using var harness = SqlServerDataQLServiceE2eTestHarness.Create(_fixture);

        var response = await harness.Service.ExecuteAsync<DepartmentStatsRow>(
            SqlServerTestEnvironment.SourceKey,
            "Employees",
            new QueryRequest
            {
                Order = [new OrderClause { Field = "Department", Direction = "asc" }],
                Group = new GroupRequest
                {
                    GroupBy = ["Department"],
                    Metrics =
                    [
                        new GroupMetricRequest { Field = "*", Operation = "count", Alias = "employees" },
                        new GroupMetricRequest { Field = "Age", Operation = "sum", Alias = "totalAge" },
                        new GroupMetricRequest { Field = "Age", Operation = "avg", Alias = "averageAge" },
                        new GroupMetricRequest { Field = "Age", Operation = "min", Alias = "minAge" },
                        new GroupMetricRequest { Field = "Age", Operation = "max", Alias = "maxAge" }
                    ]
                }
            });

        Assert.Equal(2, response.Results.Count);

        var engineering = response.Results[0];
        Assert.Equal("Engineering", engineering.Department);
        Assert.Equal(3, engineering.Employees);
        Assert.Equal(65, engineering.TotalAge);
        Assert.Equal(65.0 / 3.0, engineering.AverageAge, precision: 5);
        Assert.Equal(19, engineering.MinAge);
        Assert.Equal(24, engineering.MaxAge);

        var sales = response.Results[1];
        Assert.Equal("Sales", sales.Department);
        Assert.Equal(1, sales.Employees);
        Assert.Equal(31, sales.TotalAge);
        Assert.Equal(31.0, sales.AverageAge, precision: 5);
        Assert.Equal(31, sales.MinAge);
        Assert.Equal(31, sales.MaxAge);
    }

    [SqlServerAvailableFact]
    public async Task ExecuteAsync_WithGroupAndWhereFilter_AppliesFilterBeforeAggregation()
    {
        await using var harness = SqlServerDataQLServiceE2eTestHarness.Create(_fixture);

        var response = await harness.Service.ExecuteAsync<DepartmentStatsRow>(
            SqlServerTestEnvironment.SourceKey,
            "Employees",
            new QueryRequest
            {
                Where = QueryFilterBuilder.Field("IsActive").Eq(true).ToJsonElement(),
                Order = [new OrderClause { Field = "Department", Direction = "asc" }],
                Group = new GroupRequest
                {
                    GroupBy = ["Department"],
                    Metrics =
                    [
                        new GroupMetricRequest { Field = "*", Operation = "count", Alias = "employees" },
                        new GroupMetricRequest { Field = "Age", Operation = "sum", Alias = "totalAge" },
                        new GroupMetricRequest { Field = "Age", Operation = "min", Alias = "minAge" },
                        new GroupMetricRequest { Field = "Age", Operation = "max", Alias = "maxAge" }
                    ]
                }
            });

        // Active only: Engineering = Asha(19)+Arun(24), Sales = Riya(31). Karan excluded.
        Assert.Equal(2, response.Results.Count);

        var engineering = response.Results[0];
        Assert.Equal("Engineering", engineering.Department);
        Assert.Equal(2, engineering.Employees);
        Assert.Equal(43, engineering.TotalAge);
        Assert.Equal(19, engineering.MinAge);
        Assert.Equal(24, engineering.MaxAge);

        var sales = response.Results[1];
        Assert.Equal("Sales", sales.Department);
        Assert.Equal(1, sales.Employees);
        Assert.Equal(31, sales.TotalAge);
    }

    [SqlServerAvailableFact]
    public async Task ExecuteAsync_WithGroupByCity_ReturnsCityCounts()
    {
        await using var harness = SqlServerDataQLServiceE2eTestHarness.Create(_fixture);

        var response = await harness.Service.ExecuteAsync<CityCountRow>(
            SqlServerTestEnvironment.SourceKey,
            "Employees",
            new QueryRequest
            {
                Order = [new OrderClause { Field = "City", Direction = "asc" }],
                Group = new GroupRequest
                {
                    GroupBy = ["City"],
                    Metrics =
                    [
                        new GroupMetricRequest
                        {
                            Field = "*",
                            Operation = "count",
                            Alias = "employees"
                        }
                    ]
                }
            });

        Assert.Equal(3, response.Results.Count);
        Assert.Equal("Bengaluru", response.Results[0].City);
        Assert.Equal(1, response.Results[0].Employees);
        Assert.Equal("Delhi", response.Results[1].City);
        Assert.Equal(2, response.Results[1].Employees);
        Assert.Equal("Pune", response.Results[2].City);
        Assert.Equal(1, response.Results[2].Employees);
    }

    [SqlServerAvailableFact]
    public async Task ExecuteAsync_WithHavingOnCountAlias_FiltersGroupedRows()
    {
        await using var harness = SqlServerDataQLServiceE2eTestHarness.Create(_fixture);

        var response = await harness.Service.ExecuteAsync<DepartmentCountRow>(
            SqlServerTestEnvironment.SourceKey,
            "Employees",
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
                            Alias = "employees"
                        }
                    ],
                    Having = QueryFilterBuilder.Field("employees").Gte(2).ToJsonElement()
                }
            });

        Assert.Single(response.Results);
        Assert.Equal("Engineering", response.Results[0].Department);
        Assert.Equal(3, response.Results[0].Employees);
    }

    [SqlServerAvailableFact]
    public async Task ExecuteAsync_WithWhereAndHaving_AppliesBothFilters()
    {
        await using var harness = SqlServerDataQLServiceE2eTestHarness.Create(_fixture);

        var response = await harness.Service.ExecuteAsync<DepartmentCountRow>(
            SqlServerTestEnvironment.SourceKey,
            "Employees",
            new QueryRequest
            {
                Where = QueryFilterBuilder.Field("IsActive").Eq(true).ToJsonElement(),
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
                            Alias = "employees"
                        }
                    ],
                    Having = QueryFilterBuilder.Field("employees").Gte(2).ToJsonElement()
                }
            });

        // Active only: Engineering=2, Sales=1 → having keeps Engineering.
        Assert.Single(response.Results);
        Assert.Equal("Engineering", response.Results[0].Department);
        Assert.Equal(2, response.Results[0].Employees);
    }

    [SqlServerAvailableFact]
    public async Task ExecuteAsync_WithHavingOnSumAlias_FiltersByTotalAge()
    {
        await using var harness = SqlServerDataQLServiceE2eTestHarness.Create(_fixture);

        var response = await harness.Service.ExecuteAsync<DepartmentSumRow>(
            SqlServerTestEnvironment.SourceKey,
            "Employees",
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
                            Field = "Age",
                            Operation = "sum",
                            Alias = "totalAge"
                        }
                    ],
                    Having = QueryFilterBuilder.Field("totalAge").Gt(50).ToJsonElement()
                }
            });

        Assert.Single(response.Results);
        Assert.Equal("Engineering", response.Results[0].Department);
        Assert.Equal(65, response.Results[0].TotalAge);
    }

    [SqlServerAvailableFact]
    public async Task ExecuteAsync_WithUnsupportedFirstMetric_ThrowsAstValidationException()
    {
        await using var harness = SqlServerDataQLServiceE2eTestHarness.Create(_fixture);

        var request = new QueryRequest
        {
            Order = [new OrderClause { Field = "Department", Direction = "asc" }],
            Group = new GroupRequest
            {
                GroupBy = ["Department"],
                Metrics =
                [
                    new GroupMetricRequest
                    {
                        Field = "Name",
                        Operation = "first",
                        Alias = "firstName"
                    }
                ]
            }
        };

        var ex = await Assert.ThrowsAsync<AstValidationException>(() =>
            harness.Service.ExecuteAsync<DepartmentCountRow>(SqlServerTestEnvironment.SourceKey, "Employees", request));

        Assert.Equal("Capability.GroupOperationNotSupported", Assert.Single(ex.Errors).Code);
    }

    [SqlServerAvailableFact]
    public async Task ExecuteAsync_WithUnsupportedLastMetric_ThrowsAstValidationException()
    {
        await using var harness = SqlServerDataQLServiceE2eTestHarness.Create(_fixture);

        var request = new QueryRequest
        {
            Order = [new OrderClause { Field = "Department", Direction = "asc" }],
            Group = new GroupRequest
            {
                GroupBy = ["Department"],
                Metrics =
                [
                    new GroupMetricRequest
                    {
                        Field = "Name",
                        Operation = "last",
                        Alias = "lastName"
                    }
                ]
            }
        };

        var ex = await Assert.ThrowsAsync<AstValidationException>(() =>
            harness.Service.ExecuteAsync<DepartmentCountRow>(SqlServerTestEnvironment.SourceKey, "Employees", request));

        Assert.Equal("Capability.GroupOperationNotSupported", Assert.Single(ex.Errors).Code);
    }

    private sealed class DepartmentCountRow
    {
        public string Department { get; init; } = string.Empty;
        public long Employees { get; init; }
    }

    private sealed class DepartmentSumRow
    {
        public string Department { get; init; } = string.Empty;
        public long TotalAge { get; init; }
    }

    private sealed class DepartmentAvgRow
    {
        public string Department { get; init; } = string.Empty;
        public double AverageAge { get; init; }
    }

    private sealed class DepartmentMinRow
    {
        public string Department { get; init; } = string.Empty;
        public long MinAge { get; init; }
    }

    private sealed class DepartmentMaxRow
    {
        public string Department { get; init; } = string.Empty;
        public long MaxAge { get; init; }
    }

    private sealed class DepartmentStatsRow
    {
        public string Department { get; init; } = string.Empty;
        public long Employees { get; init; }
        public long TotalAge { get; init; }
        public double AverageAge { get; init; }
        public long MinAge { get; init; }
        public long MaxAge { get; init; }
    }

    private sealed class CityCountRow
    {
        public string City { get; init; } = string.Empty;
        public long Employees { get; init; }
    }
}
