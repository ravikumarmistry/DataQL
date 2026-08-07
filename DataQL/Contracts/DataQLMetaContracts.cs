using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataQL.Contracts;

public sealed record DataQLSourceInfo(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("provider")] string Provider);

public sealed record DataQLTableInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("schema")] string? Schema = null);

public sealed class DataQLTableSchema
{
    [JsonPropertyName("sourceKey")]
    public string SourceKey { get; init; } = string.Empty;

    [JsonPropertyName("table")]
    public string Table { get; init; } = string.Empty;

    [JsonPropertyName("provider")]
    public string Provider { get; init; } = string.Empty;

    /// <summary>
    /// JSON Schema (draft-compatible) describing the table as an object with properties.
    /// </summary>
    [JsonPropertyName("schema")]
    public JsonElement Schema { get; init; }
}
