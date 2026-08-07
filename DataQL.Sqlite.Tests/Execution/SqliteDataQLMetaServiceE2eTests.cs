using System.Text.Json;
using DataQL.Abstractions;

namespace DataQL.Sqlite.Tests.Execution;

public class SqliteDataQLMetaServiceE2eTests
{
    [Fact]
    public async Task ListSourcesAsync_ReturnsRegisteredSources()
    {
        await using var harness = SqliteDataQLServiceE2eTestHarness.Create();

        var sources = await harness.MetaService.ListSourcesAsync();

        Assert.Single(sources);
        Assert.Equal("sample", sources[0].Key);
        Assert.Equal(ProviderName.Sqlite, sources[0].Provider);
    }

    [Fact]
    public async Task ListTablesAsync_ReturnsSeededTables()
    {
        await using var harness = SqliteDataQLServiceE2eTestHarness.Create();

        var tables = await harness.MetaService.ListTablesAsync("sample");

        Assert.Contains(tables, t => t.Name == "Employees");
    }

    [Fact]
    public async Task GetTableSchemaAsync_ReturnsJsonSchemaForEmployees()
    {
        await using var harness = SqliteDataQLServiceE2eTestHarness.Create();

        var schema = await harness.MetaService.GetTableSchemaAsync("sample", "Employees");

        Assert.Equal("sample", schema.SourceKey);
        Assert.Equal("Employees", schema.Table);
        Assert.Equal(ProviderName.Sqlite, schema.Provider);
        Assert.Equal(JsonValueKind.Object, schema.Schema.ValueKind);
        Assert.True(schema.Schema.TryGetProperty("properties", out var properties));
        Assert.True(properties.TryGetProperty("Name", out var nameProperty));
        Assert.Equal("string", nameProperty.GetProperty("type").GetString());
        Assert.True(properties.TryGetProperty("Age", out var ageProperty));
        Assert.Equal("integer", ageProperty.GetProperty("type").GetString());
    }

    [Fact]
    public async Task GetTableSchemaAsync_AcceptsSchemaQualifiedName()
    {
        await using var harness = SqliteDataQLServiceE2eTestHarness.Create();

        var schema = await harness.MetaService.GetTableSchemaAsync("sample", "main.Employees");

        Assert.Equal("main.Employees", schema.Table);
        Assert.True(schema.Schema.TryGetProperty("properties", out var properties));
        Assert.True(properties.TryGetProperty("Id", out _));
    }
}
