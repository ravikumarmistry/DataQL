using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataQL.Cosmos.Translation;
using Microsoft.Azure.Cosmos;

namespace DataQL.Cosmos.Execution;

public interface ICosmosQueryExecutor
{
    Task<CosmosQueryPageResult<T>> ExecutePageAsync<T>(
        Container container,
        CosmosSqlTranslationResult translation,
        int? maxItemCount,
        string? feedContinuationToken,
        CancellationToken cancellationToken = default);
}

public sealed class CosmosQueryPageResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public string? ContinuationToken { get; init; }
    public double RequestCharge { get; init; }
}
