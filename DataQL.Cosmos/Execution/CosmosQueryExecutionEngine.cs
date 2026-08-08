using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DataQL.Abstractions;
using DataQL.Ast.Parsing;
using DataQL.Contracts;
using DataQL.Cosmos.Translation;
using DataQL.Cosmos.Validation;
using DataQL.Pipeline;
using DataQL.Token;
using DataQL.Validation;

namespace DataQL.Cosmos.Execution;

public sealed class CosmosQueryExecutionEngine
{
    private const string CosmosContinuationTokenKind = "cosmos-feed-v1";

    private readonly QueryProcessor _processor;
    private readonly CosmosSqlTranslator _translator;
    private readonly ICosmosQueryExecutor _executor;
    private readonly IContinuationTokenProtector _tokenProtector;
    private readonly ProviderCapabilities _capabilities;
    private readonly IProviderCapabilityValidator _capabilityValidator;
    private readonly IProviderQueryValidator _providerValidator;

    public CosmosQueryExecutionEngine(
        QueryProcessor? processor = null,
        CosmosSqlTranslator? translator = null,
        ICosmosQueryExecutor? executor = null,
        IContinuationTokenProtector? tokenProtector = null,
        ProviderCapabilities? capabilities = null,
        IProviderCapabilityValidator? capabilityValidator = null,
        IProviderQueryValidator? providerValidator = null)
    {
        _processor = processor ?? new QueryProcessor(
            new QueryRequestValidator(),
            new QueryAstParser(),
            new AstSemanticValidator());
        _translator = translator ?? new CosmosSqlTranslator();
        _executor = executor ?? new CosmosQueryExecutor();
        _tokenProtector = tokenProtector ?? new Base64ContinuationTokenProtector();
        _capabilities = capabilities ?? new CosmosQueryTranslator().Capabilities;
        _capabilityValidator = capabilityValidator ?? ProviderCapabilityValidator.Instance;
        _providerValidator = providerValidator ?? CosmosProviderQueryValidator.Instance;
    }

    public async Task<QueryResponse<T>> ExecuteAsync<T>(
        CosmosDataQLSession session,
        QuerySource source,
        QueryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        ValidateSource(source);

        var ast = _processor.Process(request);
        _capabilityValidator.EnsureValid(ast, _capabilities);
        _providerValidator.EnsureValid(ast, request, _capabilities);

        var containerName = source.Name.Trim();
        var queryShapeHash = BuildQueryShapeHash(containerName, request);
        var translation = _translator.Translate(ast);

        var feedToken = translation.IsGrouped
            ? null
            : DecodeFeedTokenOrThrow(request.ContinuationToken, queryShapeHash);
        var container = session.GetContainer(containerName);

        long? count = null;
        double? countRequestCharge = null;
        if (request.IncludeCount && !translation.IsGrouped)
        {
            var countTranslation = _translator.TranslateCount(ast);
            var countResult = await _executor.ExecuteCountAsync(container, countTranslation, cancellationToken);
            count = countResult.Count;
            countRequestCharge = countResult.RequestCharge;
        }

        // Grouped queries ignore limit (full aggregate set). Non-group uses MaxItemCount paging.
        var maxItemCount = translation.IsGrouped
            ? null
            : request.Limit is > 0 ? request.Limit : null;

        var page = await _executor.ExecutePageAsync<T>(
            container,
            translation,
            maxItemCount,
            feedContinuationToken: feedToken,
            cancellationToken);

        // Grouped Cosmos SQL cannot ORDER BY aggregate compositions; honor order in-memory.
        var results = request.Group is not null && request.Order.Count > 0
            ? ApplyClientOrder(page.Items, request.Order)
            : page.Items;

        var hasMore = !translation.IsGrouped && !string.IsNullOrWhiteSpace(page.ContinuationToken);
        string? continuationToken = null;
        if (hasMore)
        {
            continuationToken = EncodeFeedToken(queryShapeHash, page.ContinuationToken!);
        }

        var extensions = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["requestCharge"] = JsonSerializer.SerializeToElement(page.RequestCharge)
        };
        if (countRequestCharge is not null)
        {
            extensions["countRequestCharge"] = JsonSerializer.SerializeToElement(countRequestCharge.Value);
        }

        return new QueryResponse<T>
        {
            Results = results,
            HasMore = hasMore,
            ContinuationToken = continuationToken,
            Count = count,
            Meta = new QueryExecutionMeta
            {
                Provider = ProviderName.Cosmos,
                ExecutionTimeMs = 0,
                Extensions = extensions
            }
        };
    }

    private string? DecodeFeedTokenOrThrow(string? continuationToken, string expectedQueryShapeHash)
    {
        if (string.IsNullOrWhiteSpace(continuationToken))
        {
            return null;
        }

        if (!_tokenProtector.TryUnprotect(continuationToken, out var envelope) || envelope is null)
        {
            throw new ArgumentException("Invalid continuation token.", nameof(continuationToken));
        }

        if (!string.Equals(envelope.Provider, ProviderName.Cosmos, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Continuation token provider mismatch.", nameof(continuationToken));
        }

        if (!string.Equals(envelope.QueryShapeHash, expectedQueryShapeHash, StringComparison.Ordinal))
        {
            throw new ArgumentException("Continuation token does not match request shape.", nameof(continuationToken));
        }

        try
        {
            var payload = JsonSerializer.Deserialize<CosmosContinuationPayload>(envelope.ProviderToken);
            if (payload is null
                || !string.Equals(payload.Kind, CosmosContinuationTokenKind, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(payload.FeedToken))
            {
                throw new ArgumentException("Invalid continuation token payload.", nameof(continuationToken));
            }

            return payload.FeedToken;
        }
        catch (JsonException)
        {
            throw new ArgumentException("Invalid continuation token payload.", nameof(continuationToken));
        }
    }

    private string EncodeFeedToken(string queryShapeHash, string feedToken)
    {
        var envelope = new ContinuationTokenEnvelope
        {
            Provider = ProviderName.Cosmos,
            QueryShapeHash = queryShapeHash,
            ProviderToken = JsonSerializer.Serialize(new CosmosContinuationPayload
            {
                Kind = CosmosContinuationTokenKind,
                FeedToken = feedToken
            })
        };

        return _tokenProtector.Protect(envelope);
    }

    private static string BuildQueryShapeHash(string containerName, QueryRequest request)
    {
        var signature = new
        {
            source = containerName,
            where = request.Where?.GetRawText(),
            order = request.Order.Select(o => new
            {
                field = (o.Field ?? string.Empty).Trim(),
                direction = (o.Direction ?? "asc").Trim().ToLowerInvariant()
            }),
            select = request.Select.Select(static s => s.Trim()),
            exclude = request.Exclude.Select(static s => s.Trim()),
            distinct = request.Distinct.Select(static s => s.Trim()),
            includeCount = request.IncludeCount,
            group = request.Group
        };

        var json = JsonSerializer.Serialize(signature);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }

    private static void ValidateSource(QuerySource source)
    {
        if (!string.Equals(source.Provider, ProviderName.Cosmos, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Source provider '{source.Provider}' is not valid for '{ProviderName.Cosmos}'.", nameof(source));
        }

        if (string.IsNullOrWhiteSpace(source.Name))
        {
            throw new ArgumentException("Source name (container) is required.", nameof(source));
        }
    }

    private static IReadOnlyList<T> ApplyClientOrder<T>(IReadOnlyList<T> items, IReadOnlyList<OrderClause> order)
    {
        if (items.Count <= 1)
        {
            return items;
        }

        var decorated = items
            .Select(item => (Item: item, Keys: order.Select(o => ReadSortKey(item, o.Field)).ToArray()))
            .ToList();

        decorated.Sort((left, right) =>
        {
            for (var i = 0; i < order.Count; i++)
            {
                var comparison = Comparer<object?>.Default.Compare(left.Keys[i], right.Keys[i]);
                if (comparison == 0)
                {
                    continue;
                }

                return string.Equals(order[i].Direction, "desc", StringComparison.OrdinalIgnoreCase)
                    ? -comparison
                    : comparison;
            }

            return 0;
        });

        return decorated.Select(static d => d.Item).ToList();
    }

    private static object? ReadSortKey<T>(T item, string field)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            return null;
        }

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(item));
        if (!TryGetPropertyIgnoreCase(document.RootElement, field.Trim(), out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number when property.TryGetInt64(out var l) => l,
            JsonValueKind.Number when property.TryGetDouble(out var d) => d,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => property.GetRawText()
        };
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement property)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            property = default;
            return false;
        }

        foreach (var candidate in element.EnumerateObject())
        {
            if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                property = candidate.Value;
                return true;
            }
        }

        property = default;
        return false;
    }

    private sealed class CosmosContinuationPayload
    {
        public string Kind { get; init; } = string.Empty;
        public string FeedToken { get; init; } = string.Empty;
    }
}
