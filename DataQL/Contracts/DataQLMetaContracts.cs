using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataQL.Contracts;

public sealed class DataQLSourceInfo
{
    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    [JsonPropertyName("provider")]
    public string Provider { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    [JsonPropertyName("capabilities")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DataQLProviderCapabilitiesInfo? Capabilities { get; init; }
}

public sealed class DataQLProviderCapabilitiesInfo
{
    [JsonPropertyName("provider")]
    public string Provider { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    [JsonPropertyName("supportedOperators")]
    public IReadOnlyList<string> SupportedOperators { get; init; } = [];

    [JsonPropertyName("supportsSelect")]
    public bool SupportsSelect { get; init; }

    [JsonPropertyName("supportsExclude")]
    public bool SupportsExclude { get; init; }

    [JsonPropertyName("supportsGrouping")]
    public bool SupportsGrouping { get; init; }

    [JsonPropertyName("supportsHaving")]
    public bool SupportsHaving { get; init; }

    [JsonPropertyName("supportsNestedFields")]
    public bool SupportsNestedFields { get; init; }

    [JsonPropertyName("supportsDistinct")]
    public bool SupportsDistinct { get; init; }

    [JsonPropertyName("supportedGroupOperations")]
    public IReadOnlyList<string> SupportedGroupOperations { get; init; } = [];

    [JsonPropertyName("notes")]
    public IReadOnlyList<DataQLCapabilityNoteInfo> Notes { get; init; } = [];
}

public sealed class DataQLCapabilityNoteInfo
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; init; } = "info";

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}

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
