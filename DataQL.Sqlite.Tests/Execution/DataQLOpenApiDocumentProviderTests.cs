using System.Text.Json;

namespace DataQL.Sqlite.Tests.Execution;

public class DataQLOpenApiDocumentProviderTests
{
    [Fact]
    public async Task GetDocumentAsync_IncludesMetaPathsAndEmployeesQuery()
    {
        await using var harness = SqliteDataQLServiceE2eTestHarness.Create();

        var document = await harness.OpenApi.GetDocumentAsync("/dataql");

        Assert.Equal("3.0.3", document.GetProperty("openapi").GetString());
        Assert.True(document.TryGetProperty("paths", out var paths));
        Assert.True(paths.TryGetProperty("/dataql/meta/sources", out _));
        Assert.True(paths.TryGetProperty("/dataql/meta/openapi.json", out _));
        Assert.True(paths.TryGetProperty("/dataql/query/{sourceKey}/{table}", out _));
        Assert.True(paths.TryGetProperty("/dataql/query/sample/Employees", out _));

        Assert.True(document.TryGetProperty("components", out var components));
        Assert.True(components.TryGetProperty("schemas", out var schemas));
        Assert.True(schemas.TryGetProperty("QueryRequest", out _));
        Assert.True(schemas.TryGetProperty("sample__Employees", out var employeesSchema));
        Assert.True(employeesSchema.TryGetProperty("properties", out var properties));
        Assert.True(properties.TryGetProperty("Name", out _));
        Assert.True(properties.TryGetProperty("Age", out _));
    }

    [Fact]
    public async Task GetDocumentAsync_CachesUntilRefresh()
    {
        await using var harness = SqliteDataQLServiceE2eTestHarness.Create();

        var first = await harness.OpenApi.GetDocumentAsync("/dataql");
        var second = await harness.OpenApi.GetDocumentAsync("/dataql");
        Assert.True(JsonElement.DeepEquals(first, second));

        var refreshed = await harness.OpenApi.GetDocumentAsync("/dataql", refresh: true);
        Assert.Equal("3.0.3", refreshed.GetProperty("openapi").GetString());
        Assert.True(refreshed.TryGetProperty("paths", out var paths));
        Assert.True(paths.TryGetProperty("/dataql/query/sample/Employees", out _));
    }
}
