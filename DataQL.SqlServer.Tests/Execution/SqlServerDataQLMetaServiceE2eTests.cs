using System.Text.Json;
using DataQL.Abstractions;
using DataQL.SqlServer.Tests.Infrastructure;
using Microsoft.Data.SqlClient;

namespace DataQL.SqlServer.Tests.Execution;

[Collection(SqlServerE2eCollection.Name)]
public class SqlServerDataQLMetaServiceE2eTests
{
    private readonly SqlServerE2eFixture _fixture;

    public SqlServerDataQLMetaServiceE2eTests(SqlServerE2eFixture fixture)
    {
        _fixture = fixture;
    }

    [SqlServerAvailableFact]
    public async Task ListSourcesAsync_ReturnsRegisteredSources()
    {
        await using var harness = SqlServerDataQLServiceE2eTestHarness.Create(_fixture);

        var sources = await harness.MetaService.ListSourcesAsync();

        Assert.Single(sources);
        Assert.Equal(SqlServerTestEnvironment.SourceKey, sources[0].Key);
        Assert.Equal(ProviderName.SqlServer, sources[0].Provider);
    }

    [SqlServerAvailableFact]
    public async Task ListTablesAsync_ReturnsSeededTables()
    {
        await using var harness = SqlServerDataQLServiceE2eTestHarness.Create(_fixture);

        var tables = await harness.MetaService.ListTablesAsync(SqlServerTestEnvironment.SourceKey);

        Assert.Contains(tables, t => t.Name is "dbo.Employees" or "Employees");
    }

    [SqlServerAvailableFact]
    public async Task GetTableSchemaAsync_ReturnsJsonSchemaForEmployees()
    {
        await using var harness = SqlServerDataQLServiceE2eTestHarness.Create(_fixture);

        var schema = await harness.MetaService.GetTableSchemaAsync(SqlServerTestEnvironment.SourceKey, "Employees");

        Assert.Equal(SqlServerTestEnvironment.SourceKey, schema.SourceKey);
        Assert.Equal(ProviderName.SqlServer, schema.Provider);
        Assert.Equal(JsonValueKind.Object, schema.Schema.ValueKind);
        Assert.True(schema.Schema.TryGetProperty("properties", out var properties));
        Assert.True(properties.TryGetProperty("Name", out var nameProperty));
        Assert.Equal("string", nameProperty.GetProperty("type").GetString());
        Assert.True(properties.TryGetProperty("Age", out var ageProperty));
        Assert.Equal("integer", ageProperty.GetProperty("type").GetString());
        Assert.True(properties.TryGetProperty("IsActive", out var isActiveProperty));
        Assert.Equal("boolean", isActiveProperty.GetProperty("type").GetString());
        Assert.True(properties.TryGetProperty("CreatedAt", out var createdAtProperty));
        Assert.Equal("string", createdAtProperty.GetProperty("type").GetString());
        Assert.Equal("date-time", createdAtProperty.GetProperty("format").GetString());
        Assert.True(properties.TryGetProperty("Tags", out var tagsProperty));
        Assert.Equal("string", tagsProperty.GetProperty("type").GetString());
        Assert.True(properties.TryGetProperty("Projects", out _));
    }

    [SqlServerAvailableFact]
    public async Task GetTableSchemaAsync_AcceptsSchemaQualifiedName()
    {
        await using var harness = SqlServerDataQLServiceE2eTestHarness.Create(_fixture);

        var schema = await harness.MetaService.GetTableSchemaAsync(
            SqlServerTestEnvironment.SourceKey,
            "dbo.Employees");

        Assert.Equal("dbo.Employees", schema.Table);
        Assert.True(schema.Schema.TryGetProperty("properties", out var properties));
        Assert.True(properties.TryGetProperty("Id", out _));
    }

    [SqlServerAvailableFact]
    public async Task GetTableSchemaAsync_WithMissingTable_ThrowsInvalidOperationException()
    {
        await using var harness = SqlServerDataQLServiceE2eTestHarness.Create(_fixture);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.MetaService.GetTableSchemaAsync(SqlServerTestEnvironment.SourceKey, "DoesNotExist"));

        Assert.Contains("DoesNotExist", ex.Message);
    }

    [SqlServerAvailableFact]
    public async Task Fixture_UsesDockerSeededEmployees()
    {
        Assert.False(string.IsNullOrWhiteSpace(_fixture.ConnectionString));

        await using var connection = new SqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM dbo.Employees;";
        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        Assert.True(count >= 4);
    }
}
