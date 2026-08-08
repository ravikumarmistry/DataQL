using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataQL.Contracts;

public sealed class QueryResponse<T>
{
    public IReadOnlyList<T> Results { get; init; } = [];
    public bool HasMore { get; init; }
    public string? ContinuationToken { get; init; }
    public long? Count { get; init; }

    [JsonPropertyName("_meta")]
    public QueryExecutionMeta Meta { get; init; } = null!;
}

public sealed class QueryExecutionMeta
{
    public string Provider { get; init; } = string.Empty;
    public long ExecutionTimeMs { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; set; }
}
