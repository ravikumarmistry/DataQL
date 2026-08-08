using DataQL.Contracts;
using DataQL.Cosmos.Tests.Infrastructure;
using DataQL.Validation;

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
    public async Task ExecuteAsync_Ne_ExcludesMatchingRows()
    {
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("Department").Ne("Engineering"));
        Assert.Equal(["Riya"], names);
    }

    [CosmosAvailableFact]
    public async Task ExecuteAsync_Gt_ReturnsOlderEmployees()
    {
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("Age").Gt(24));
        Assert.Equal(["Riya"], names);
    }

    [CosmosAvailableFact]
    public async Task ExecuteAsync_Gte_ReturnsAgeAtLeastTwentyFour()
    {
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("Age").Gte(24));
        Assert.Equal(["Arun", "Riya"], names);
    }

    [CosmosAvailableFact]
    public async Task ExecuteAsync_Lt_ReturnsYoungerEmployees()
    {
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("Age").Lt(22));
        Assert.Equal(["Asha"], names);
    }

    [CosmosAvailableFact]
    public async Task ExecuteAsync_Lte_ReturnsAgeAtMostTwentyTwo()
    {
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("Age").Lte(22));
        Assert.Equal(["Asha", "Karan"], names);
    }

    [CosmosAvailableFact]
    public async Task ExecuteAsync_In_ReturnsMatchingCities()
    {
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("City").In("Delhi", "Pune"));
        Assert.Equal(["Asha", "Karan", "Riya"], names);
    }

    [CosmosAvailableFact]
    public async Task ExecuteAsync_Nin_ExcludesMatchingCities()
    {
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("City").Nin("Delhi", "Pune"));
        Assert.Equal(["Arun"], names);
    }

    [CosmosAvailableFact]
    public async Task ExecuteAsync_Contains_MatchesSubstring()
    {
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("Name").Contains("ar"));
        Assert.Equal(["Arun", "Karan"], names);
    }

    [CosmosAvailableFact]
    public async Task ExecuteAsync_StartsWith_MatchesPrefix()
    {
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("Name").StartsWith("A"));
        Assert.Equal(["Arun", "Asha"], names);
    }

    [CosmosAvailableFact]
    public async Task ExecuteAsync_EndsWith_MatchesSuffix()
    {
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("Name").EndsWith("a"));
        Assert.Equal(["Asha", "Riya"], names);
    }

    [CosmosAvailableFact]
    public async Task ExecuteAsync_ExistsTrue_ReturnsNonNullNotes()
    {
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("Notes").Exists(true));
        Assert.Equal(["Asha", "Riya"], names);
    }

    [CosmosAvailableFact]
    public async Task ExecuteAsync_ExistsFalse_ReturnsNullNotes()
    {
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("Notes").Exists(false));
        Assert.Equal(["Arun", "Karan"], names);
    }

    [CosmosAvailableFact]
    public async Task ExecuteAsync_IsNullTrue_ReturnsNullNotes()
    {
        // Notes omitted on seed docs without a value → treated as null/undefined for IS_NULL.
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("Notes").IsNull(true));
        Assert.Equal(["Arun", "Karan"], names);
    }

    [CosmosAvailableFact]
    public async Task ExecuteAsync_IsNullFalse_ReturnsNonNullNotes()
    {
        var names = await QueryNamesAsync(QueryFilterBuilder.Field("Notes").IsNull(false));
        Assert.Equal(["Asha", "Riya"], names);
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
    public async Task ExecuteAsync_Or_CombinesPredicates()
    {
        var filter = QueryFilterBuilder.Or(
            QueryFilterBuilder.Field("City").Eq("Bengaluru"),
            QueryFilterBuilder.Field("City").Eq("Pune"));

        var names = await QueryNamesAsync(filter);
        Assert.Equal(["Arun", "Karan"], names);
    }

    [CosmosAvailableFact]
    public async Task ExecuteAsync_Not_NegatesPredicate()
    {
        var filter = QueryFilterBuilder.Not(
            QueryFilterBuilder.Field("Department").Eq("Engineering"));

        var names = await QueryNamesAsync(filter);
        Assert.Equal(["Riya"], names);
    }

    [CosmosAvailableFact]
    public async Task ExecuteAsync_WithComplexFilter_ReturnsExpectedRows()
    {
        await using var harness = CosmosDataQLServiceE2eTestHarness.Create(_fixture);

        var filter = QueryFilterBuilder.And(
            QueryFilterBuilder.Field("Age").Gte(20),
            QueryFilterBuilder.Or(
                QueryFilterBuilder.Field("City").Eq("Delhi"),
                QueryFilterBuilder.Field("Name").StartsWith("Ar")),
            QueryFilterBuilder.Not(
                QueryFilterBuilder.Field("Name").Eq("Karan")));

        var response = await harness.Service.ExecuteAsync<EmployeeRow>(
            CosmosTestEnvironment.SourceKey,
            CosmosTestEnvironment.ContainerId,
            new QueryRequest
            {
                Where = filter.ToJsonElement(),
                Order = [new OrderClause { Field = "Age", Direction = "asc" }]
            });

        Assert.Equal(2, response.Results.Count);
        Assert.Equal("Arun", response.Results[0].Name);
        Assert.Equal("Riya", response.Results[1].Name);
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

    [CosmosAvailableFact]
    public async Task ExecuteAsync_WithIncludeCount_ReturnsCountAndCountRequestCharge()
    {
        await using var harness = CosmosDataQLServiceE2eTestHarness.Create(_fixture);

        var response = await harness.Service.ExecuteAsync<EmployeeRow>(
            CosmosTestEnvironment.SourceKey,
            CosmosTestEnvironment.ContainerId,
            new QueryRequest
            {
                Where = QueryFilterBuilder.Field("Age").Gte(21).ToJsonElement(),
                Order = [new OrderClause { Field = "Age", Direction = "desc" }],
                Limit = 1,
                IncludeCount = true
            });

        Assert.Single(response.Results);
        Assert.Equal(3, response.Count);
        Assert.NotNull(response.Meta.Extensions);
        Assert.True(response.Meta.Extensions.ContainsKey("requestCharge"));
        Assert.True(response.Meta.Extensions.ContainsKey("countRequestCharge"));
        Assert.True(response.Meta.Extensions["countRequestCharge"].GetDouble() > 0);
    }

    [CosmosAvailableFact]
    public async Task ExecuteAsync_WithoutOrder_ReturnsResults()
    {
        await using var harness = CosmosDataQLServiceE2eTestHarness.Create(_fixture);

        var response = await harness.Service.ExecuteAsync<EmployeeRow>(
            CosmosTestEnvironment.SourceKey,
            CosmosTestEnvironment.ContainerId,
            new QueryRequest
            {
                Where = QueryFilterBuilder.Field("Name").Eq("Asha").ToJsonElement()
            });

        Assert.Single(response.Results);
        Assert.Equal("Asha", response.Results[0].Name);
    }

    [CosmosAvailableFact]
    public async Task ExecuteAsync_WithRegex_MatchesPattern()
    {
        var where = System.Text.Json.JsonDocument.Parse("""{"Name":{"$regex":"^A.*"}}""").RootElement.Clone();
        var names = await QueryNamesAsync(where);
        Assert.Equal(["Arun", "Asha"], names);
    }

    [CosmosAvailableFact]
    public async Task ExecuteAsync_WithExclude_ThrowsAstValidationException()
    {
        await using var harness = CosmosDataQLServiceE2eTestHarness.Create(_fixture);

        var request = new QueryRequest
        {
            Where = QueryFilterBuilder.Field("Name").Eq("Asha").ToJsonElement(),
            Order = [new OrderClause { Field = "Name", Direction = "asc" }],
            Select = ["Name", "Age"],
            Exclude = ["Age"]
        };

        var ex = await Assert.ThrowsAsync<AstValidationException>(() =>
            harness.Service.ExecuteAsync<EmployeeRow>(
                CosmosTestEnvironment.SourceKey,
                CosmosTestEnvironment.ContainerId,
                request));

        Assert.Contains(ex.Errors, e => e.Code == "Capability.ExcludeNotSupported");
        Assert.Contains(ex.Errors, e => e.Provider == "Cosmos");
    }

    [CosmosAvailableFact]
    public async Task ExecuteAsync_WithDistinct_ThrowsAstValidationException()
    {
        await using var harness = CosmosDataQLServiceE2eTestHarness.Create(_fixture);

        var request = new QueryRequest
        {
            Order = [new OrderClause { Field = "Department", Direction = "asc" }],
            Distinct = ["Department"]
        };

        var ex = await Assert.ThrowsAsync<AstValidationException>(() =>
            harness.Service.ExecuteAsync<EmployeeRow>(
                CosmosTestEnvironment.SourceKey,
                CosmosTestEnvironment.ContainerId,
                request));

        Assert.Contains(ex.Errors, e => e.Code == "Capability.DistinctNotSupported");
        Assert.Contains(ex.Errors, e => e.Provider == "Cosmos");
    }

    private async Task<IReadOnlyList<string>> QueryNamesAsync(QueryFilter filter) =>
        await QueryNamesAsync(filter.ToJsonElement());

    private async Task<IReadOnlyList<string>> QueryNamesAsync(System.Text.Json.JsonElement where)
    {
        await using var harness = CosmosDataQLServiceE2eTestHarness.Create(_fixture);

        var response = await harness.Service.ExecuteAsync<EmployeeRow>(
            CosmosTestEnvironment.SourceKey,
            CosmosTestEnvironment.ContainerId,
            new QueryRequest
            {
                Where = where,
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
