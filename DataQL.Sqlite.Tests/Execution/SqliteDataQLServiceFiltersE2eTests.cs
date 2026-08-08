using DataQL.Contracts;

namespace DataQL.Sqlite.Tests.Execution;

public class SqliteDataQLServiceFiltersE2eTests
{
    [Fact]
    public async Task ExecuteAsync_WithLimitAndIncludeCount_ReturnsTrimmedPageAndCount()
    {
        await using var harness = SqliteDataQLServiceE2eTestHarness.Create();

        var response = await harness.Service.ExecuteAsync<EmployeeRow>(
            "sample",
            "Employees",
            new QueryRequest
            {
                Where = QueryFilterBuilder.Field("Age").Gte(21).ToJsonElement(),
                Order = [new OrderClause { Field = "Age", Direction = "desc" }],
                Limit = 2,
                IncludeCount = true
            });

        Assert.Equal(2, response.Results.Count);
        Assert.Equal("Riya", response.Results[0].Name);
        Assert.Equal("Arun", response.Results[1].Name);
        Assert.True(response.HasMore);
        Assert.Equal(3, response.Count);
        Assert.NotNull(response.Meta);
        Assert.Equal("Sqlite", response.Meta.Provider);
        Assert.True(response.Meta.ExecutionTimeMs >= 0);

        Assert.Contains(
            harness.LogMessages,
            m => m.Contains("SqliteQueryExecutor", StringComparison.Ordinal)
                && m.Contains("rows", StringComparison.Ordinal)
                && m.Contains("@p0=21", StringComparison.Ordinal));
        Assert.Contains(
            harness.LogMessages,
            m => m.Contains("SqliteQueryExecutor", StringComparison.Ordinal)
                && m.Contains("count", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_WithoutWhere_LogsEmptyParameterBag()
    {
        await using var harness = SqliteDataQLServiceE2eTestHarness.Create();

        var response = await harness.Service.ExecuteAsync<EmployeeRow>(
            "sample",
            "Employees",
            new QueryRequest
            {
                Order = [new OrderClause { Field = "Name", Direction = "asc" }],
                Limit = 1
            });

        Assert.Single(response.Results);
        Assert.Contains(
            harness.LogMessages,
            m => m.Contains("SqliteQueryExecutor", StringComparison.Ordinal)
                && m.Contains("| Parameters: {}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_Eq_ReturnsMatchingRow()
    {
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("Name").Eq("Asha"));
        Assert.Equal(["Asha"], names);
    }

    [Fact]
    public async Task ExecuteAsync_Ne_ExcludesMatchingRows()
    {
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("Department").Ne("Engineering"));
        Assert.Equal(["Riya"], names);
    }

    [Fact]
    public async Task ExecuteAsync_Gt_ReturnsOlderEmployees()
    {
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("Age").Gt(24));
        Assert.Equal(["Riya"], names);
    }

    [Fact]
    public async Task ExecuteAsync_Gte_ReturnsAgeAtLeastTwentyFour()
    {
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("Age").Gte(24));
        Assert.Equal(["Arun", "Riya"], names);
    }

    [Fact]
    public async Task ExecuteAsync_Lt_ReturnsYoungerEmployees()
    {
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("Age").Lt(22));
        Assert.Equal(["Asha"], names);
    }

    [Fact]
    public async Task ExecuteAsync_Lte_ReturnsAgeAtMostTwentyTwo()
    {
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("Age").Lte(22));
        Assert.Equal(["Asha", "Karan"], names);
    }

    [Fact]
    public async Task ExecuteAsync_In_ReturnsMatchingCities()
    {
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("City").In("Delhi", "Pune"));
        Assert.Equal(["Asha", "Karan", "Riya"], names);
    }

    [Fact]
    public async Task ExecuteAsync_Nin_ExcludesMatchingCities()
    {
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("City").Nin("Delhi", "Pune"));
        Assert.Equal(["Arun"], names);
    }

    [Fact]
    public async Task ExecuteAsync_Contains_MatchesSubstring()
    {
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("Name").Contains("ar"));
        Assert.Equal(["Arun", "Karan"], names);
    }

    [Fact]
    public async Task ExecuteAsync_StartsWith_MatchesPrefix()
    {
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("Name").StartsWith("A"));
        Assert.Equal(["Arun", "Asha"], names);
    }

    [Fact]
    public async Task ExecuteAsync_EndsWith_MatchesSuffix()
    {
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("Name").EndsWith("a"));
        Assert.Equal(["Asha", "Riya"], names);
    }

    [Fact]
    public async Task ExecuteAsync_ExistsTrue_ReturnsNonNullNotes()
    {
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("Notes").Exists(true));
        Assert.Equal(["Asha", "Riya"], names);
    }

    [Fact]
    public async Task ExecuteAsync_ExistsFalse_ReturnsNullNotes()
    {
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("Notes").Exists(false));
        Assert.Equal(["Arun", "Karan"], names);
    }

    [Fact]
    public async Task ExecuteAsync_IsNullTrue_ReturnsNullNotes()
    {
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("Notes").IsNull(true));
        Assert.Equal(["Arun", "Karan"], names);
    }

    [Fact]
    public async Task ExecuteAsync_IsNullFalse_ReturnsNonNullNotes()
    {
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("Notes").IsNull(false));
        Assert.Equal(["Asha", "Riya"], names);
    }

    [Fact]
    public async Task ExecuteAsync_And_CombinesPredicates()
    {
        var filter = QueryFilterBuilder.And(
            QueryFilterBuilder.Field("Department").Eq("Engineering"),
            QueryFilterBuilder.Field("IsActive").Eq(true));

        var names = await QueryNamesAsync(filter);
        Assert.Equal(["Arun", "Asha"], names);
    }

    [Fact]
    public async Task ExecuteAsync_Or_CombinesPredicates()
    {
        var filter = QueryFilterBuilder.Or(
            QueryFilterBuilder.Field("City").Eq("Bengaluru"),
            QueryFilterBuilder.Field("City").Eq("Pune"));

        var names = await QueryNamesAsync(filter);
        Assert.Equal(["Arun", "Karan"], names);
    }

    [Fact]
    public async Task ExecuteAsync_Not_NegatesPredicate()
    {
        var filter = QueryFilterBuilder.Not(
            QueryFilterBuilder.Field("Department").Eq("Engineering"));

        var names = await QueryNamesAsync(filter);
        Assert.Equal(["Riya"], names);
    }

    [Fact]
    public async Task ExecuteAsync_WithComplexFilter_ReturnsExpectedRows()
    {
        await using var harness = SqliteDataQLServiceE2eTestHarness.Create();

        var filter = QueryFilterBuilder.And(
            QueryFilterBuilder.Field("Age").Gte(20),
            QueryFilterBuilder.Or(
                QueryFilterBuilder.Field("City").Eq("Delhi"),
                QueryFilterBuilder.Field("Name").StartsWith("Ar")),
            QueryFilterBuilder.Not(
                QueryFilterBuilder.Field("Name").Eq("Karan")));

        var response = await harness.Service.ExecuteAsync<EmployeeRow>(
            "sample",
            "Employees",
            new QueryRequest
            {
                Where = filter.ToJsonElement(),
                Order = [new OrderClause { Field = "Age", Direction = "asc" }]
            });

        Assert.Equal(2, response.Results.Count);
        Assert.Equal("Arun", response.Results[0].Name);
        Assert.Equal("Riya", response.Results[1].Name);
    }

    [Fact]
    public async Task ExecuteAsync_WithSelect_ProjectsRequestedFields()
    {
        await using var harness = SqliteDataQLServiceE2eTestHarness.Create();

        var response = await harness.Service.ExecuteAsync<NameAgeRow>(
            "sample",
            "Employees",
            new QueryRequest
            {
                Where = QueryFilterBuilder.Field("Name").Eq("Asha").ToJsonElement(),
                Order = [new OrderClause { Field = "Id", Direction = "asc" }],
                Select = ["Name", "Age"]
            });

        Assert.Single(response.Results);
        Assert.Equal("Asha", response.Results[0].Name);
        Assert.Equal(19, response.Results[0].Age);
    }

    [Fact]
    public async Task ExecuteAsync_WithSelectAndExclude_OmitsExcludedFields()
    {
        await using var harness = SqliteDataQLServiceE2eTestHarness.Create();

        var response = await harness.Service.ExecuteAsync<NameAgeRow>(
            "sample",
            "Employees",
            new QueryRequest
            {
                Where = QueryFilterBuilder.Field("Name").Eq("Asha").ToJsonElement(),
                Order = [new OrderClause { Field = "Id", Direction = "asc" }],
                Select = ["Name", "Age", "City"],
                Exclude = ["City"]
            });

        Assert.Single(response.Results);
        Assert.Equal("Asha", response.Results[0].Name);
        Assert.Equal(19, response.Results[0].Age);

        // City was excluded from the SELECT list, so mapping a wider type would not populate it.
        // Verify SQL projection by ensuring a City-only type gets no value when City is excluded.
        var cityResponse = await harness.Service.ExecuteAsync<CityOnlyRow>(
            "sample",
            "Employees",
            new QueryRequest
            {
                Where = QueryFilterBuilder.Field("Name").Eq("Asha").ToJsonElement(),
                Order = [new OrderClause { Field = "Id", Direction = "asc" }],
                Select = ["Name", "Age", "City"],
                Exclude = ["City"]
            });

        Assert.Single(cityResponse.Results);
        Assert.True(string.IsNullOrEmpty(cityResponse.Results[0].City));
    }

    [Fact]
    public async Task ExecuteAsync_WithUnsupportedRegex_ThrowsAstValidationException()
    {
        await using var harness = SqliteDataQLServiceE2eTestHarness.Create();

        var where = System.Text.Json.JsonDocument.Parse("""{"Name":{"$regex":"Ada.*"}}""").RootElement.Clone();
        var request = new QueryRequest
        {
            Where = where,
            Order = [new OrderClause { Field = "Name", Direction = "asc" }]
        };

        var ex = await Assert.ThrowsAsync<DataQL.Validation.AstValidationException>(() =>
            harness.Service.ExecuteAsync<EmployeeRow>("sample", "Employees", request));

        var error = Assert.Single(ex.Errors);
        Assert.Equal("Capability.OperatorNotSupported", error.Code);
        Assert.Equal("Sqlite", error.Provider);
    }

    [Fact]
    public async Task ExecuteAsync_WithNestedField_ThrowsAstValidationException()
    {
        await using var harness = SqliteDataQLServiceE2eTestHarness.Create();

        var where = System.Text.Json.JsonDocument.Parse("""{"Address.City":"Delhi"}""").RootElement.Clone();
        var request = new QueryRequest
        {
            Where = where,
            Order = [new OrderClause { Field = "Name", Direction = "asc" }]
        };

        var ex = await Assert.ThrowsAsync<DataQL.Validation.AstValidationException>(() =>
            harness.Service.ExecuteAsync<EmployeeRow>("sample", "Employees", request));

        var error = Assert.Single(ex.Errors);
        Assert.Equal("Capability.NestedFieldsNotSupported", error.Code);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnsupportedSize_ThrowsAstValidationException()
    {
        await using var harness = SqliteDataQLServiceE2eTestHarness.Create();

        var where = System.Text.Json.JsonDocument.Parse("""{"Tags":{"$size":2}}""").RootElement.Clone();
        var request = new QueryRequest
        {
            Where = where,
            Order = [new OrderClause { Field = "Name", Direction = "asc" }]
        };

        var ex = await Assert.ThrowsAsync<DataQL.Validation.AstValidationException>(() =>
            harness.Service.ExecuteAsync<EmployeeRow>("sample", "Employees", request));

        var error = Assert.Single(ex.Errors);
        Assert.Equal("Capability.OperatorNotSupported", error.Code);
    }

    private static async Task<IReadOnlyList<string>> QueryNamesAsync(QueryFilter filter)
    {
        await using var harness = SqliteDataQLServiceE2eTestHarness.Create();

        var response = await harness.Service.ExecuteAsync<EmployeeRow>(
            "sample",
            "Employees",
            new QueryRequest
            {
                Where = filter.ToJsonElement(),
                Order = [new OrderClause { Field = "Name", Direction = "asc" }]
            });

        return response.Results.Select(static r => r.Name).ToArray();
    }

    private sealed class EmployeeRow
    {
        public int Id { get; init; }
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

    private sealed class CityOnlyRow
    {
        public string? City { get; init; }
    }
}
