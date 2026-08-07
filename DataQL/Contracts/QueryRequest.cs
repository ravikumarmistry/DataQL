using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataQL.Contracts;

public sealed class QueryRequest
{
    [JsonPropertyName("where")]
    public JsonElement? Where { get; init; }

    [JsonPropertyName("order")]
    public IReadOnlyList<OrderClause> Order { get; init; } = [];

    [JsonPropertyName("select")]
    public IReadOnlyList<string> Select { get; init; } = [];

    [JsonPropertyName("exclude")]
    public IReadOnlyList<string> Exclude { get; init; } = [];

    [JsonPropertyName("distinct")]
    public IReadOnlyList<string> Distinct { get; init; } = [];

    [JsonPropertyName("limit")]
    public int? Limit { get; init; }

    [JsonPropertyName("continuationToken")]
    public string? ContinuationToken { get; init; }

    [JsonPropertyName("includeCount")]
    public bool IncludeCount { get; init; }

    [JsonPropertyName("group")]
    public GroupRequest? Group { get; init; }
}

public sealed class GroupRequest
{
    [JsonPropertyName("groupBy")]
    public IReadOnlyList<string> GroupBy { get; init; } = [];

    [JsonPropertyName("metrics")]
    public IReadOnlyList<GroupMetricRequest> Metrics { get; init; } = [];

    [JsonPropertyName("having")]
    public JsonElement? Having { get; init; }
}

public sealed class GroupMetricRequest
{
    [JsonPropertyName("field")]
    public string Field { get; init; } = string.Empty;

    [JsonPropertyName("operation")]
    public string Operation { get; init; } = string.Empty;

    [JsonPropertyName("alias")]
    public string Alias { get; init; } = string.Empty;
}

public sealed class OrderClause
{
    [JsonPropertyName("field")]
    public string Field { get; init; } = string.Empty;

    [JsonPropertyName("direction")]
    public string Direction { get; init; } = "asc";
}
