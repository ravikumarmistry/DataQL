using System;
using System.Collections.Generic;
using System.Linq;
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

        var definition = BuildDefinition(translation);

        _logger?.LogInformation(
            "DataQL Cosmos query: {Sql} | MaxItemCount={MaxItemCount} | HasFeedToken={HasFeedToken} | Grouped={Grouped}",
            translation.Sql,
            maxItemCount,
            !string.IsNullOrWhiteSpace(feedContinuationToken),
            translation.IsGrouped);

        // GROUP BY: drain all results; limit/MaxItemCount has no effect; no continuation token.
        if (translation.IsGrouped)
        {
            return await ExecuteGroupedAsync<T>(container, definition, cancellationToken);
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

    public async Task<CosmosCountResult> ExecuteCountAsync(
        Container container,
        CosmosSqlTranslationResult translation,
        CancellationToken cancellationToken = default)
    {
        if (translation.IsGrouped)
        {
            throw new NotSupportedException("Cosmos grouped queries do not support includeCount.");
        }

        var definition = BuildDefinition(translation);

        _logger?.LogInformation("DataQL Cosmos count query: {Sql}", translation.Sql);

        using var iterator = container.GetItemQueryIterator<long>(definition);
        double requestCharge = 0;
        long count = 0;

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            requestCharge += response.RequestCharge;
            if (response.Count > 0)
            {
                count = response.Resource.FirstOrDefault();
            }
        }

        return new CosmosCountResult
        {
            Count = count,
            RequestCharge = requestCharge
        };
    }

    private static QueryDefinition BuildDefinition(CosmosSqlTranslationResult translation)
    {
        var definition = new QueryDefinition(translation.Sql);
        foreach (var pair in translation.Parameters)
        {
            definition = definition.WithParameter(pair.Key, pair.Value);
        }

        return definition;
    }

    private static async Task<CosmosQueryPageResult<T>> ExecuteGroupedAsync<T>(
        Container container,
        QueryDefinition definition,
        CancellationToken cancellationToken)
    {
        using var iterator = container.GetItemQueryIterator<T>(definition);

        var items = new List<T>();
        double requestCharge = 0;

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            requestCharge += response.RequestCharge;
            items.AddRange(response);
        }

        return new CosmosQueryPageResult<T>
        {
            Items = items,
            ContinuationToken = null,
            RequestCharge = requestCharge
        };
    }
}
