using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataQL.Cosmos.Translation;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace DataQL.Cosmos.Execution;

public sealed class CosmosQueryExecutor(
    ILogger<CosmosQueryExecutor>? logger = null) : ICosmosQueryExecutor
{
    private readonly ILogger<CosmosQueryExecutor>? _logger = logger;

    public async Task<CosmosQueryPageResult<T>> ExecutePageAsync<T>(
        Container container,
        CosmosSqlTranslationResult translation,
        int? maxItemCount,
        string? feedContinuationToken,
        CancellationToken cancellationToken = default)
    {
        if (translation.IsGrouped && !string.IsNullOrWhiteSpace(feedContinuationToken))
        {
            throw new NotSupportedException("Cosmos grouped queries do not support continuation tokens.");
        }

        var definition = new QueryDefinition(translation.Sql);
        foreach (var pair in translation.Parameters)
        {
            definition = definition.WithParameter(pair.Key, pair.Value);
        }

        _logger?.LogInformation(
            "DataQL Cosmos query: {Sql} | MaxItemCount={MaxItemCount} | HasFeedToken={HasFeedToken} | Grouped={Grouped}",
            translation.Sql,
            maxItemCount,
            !string.IsNullOrWhiteSpace(feedContinuationToken),
            translation.IsGrouped);

        // GROUP BY cannot use feed continuation; drain the iterator without reading ContinuationToken.
        if (translation.IsGrouped)
        {
            return await ExecuteGroupedAsync<T>(container, definition, maxItemCount, cancellationToken);
        }

        using var iterator = container.GetItemQueryIterator<T>(
            definition,
            continuationToken: string.IsNullOrWhiteSpace(feedContinuationToken) ? null : feedContinuationToken,
            requestOptions: new QueryRequestOptions
            {
                MaxItemCount = maxItemCount
            });

        if (!iterator.HasMoreResults)
        {
            return new CosmosQueryPageResult<T>
            {
                Items = [],
                ContinuationToken = null,
                RequestCharge = 0
            };
        }

        var response = await iterator.ReadNextAsync(cancellationToken);
        var items = new List<T>(response.Count);
        items.AddRange(response);

        return new CosmosQueryPageResult<T>
        {
            Items = items,
            ContinuationToken = response.ContinuationToken,
            RequestCharge = response.RequestCharge
        };
    }

    private static async Task<CosmosQueryPageResult<T>> ExecuteGroupedAsync<T>(
        Container container,
        QueryDefinition definition,
        int? maxItemCount,
        CancellationToken cancellationToken)
    {
        using var iterator = container.GetItemQueryIterator<T>(
            definition,
            requestOptions: new QueryRequestOptions
            {
                MaxItemCount = maxItemCount
            });

        var items = new List<T>();
        double requestCharge = 0;

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            requestCharge += response.RequestCharge;
            items.AddRange(response);
        }

        if (maxItemCount is > 0 && items.Count > maxItemCount.Value)
        {
            items = items.GetRange(0, maxItemCount.Value);
        }

        return new CosmosQueryPageResult<T>
        {
            Items = items,
            ContinuationToken = null,
            RequestCharge = requestCharge
        };
    }
}
