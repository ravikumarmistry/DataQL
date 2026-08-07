using System.Collections.Generic;

namespace DataQL.Contracts;

public sealed class QueryResponse<T>
{
    public IReadOnlyList<T> Results { get; init; } = [];
    public bool HasMore { get; init; }
    public string? ContinuationToken { get; init; }
    public long? Count { get; init; }
}
