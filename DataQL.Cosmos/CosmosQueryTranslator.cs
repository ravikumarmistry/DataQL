using System.Collections.Generic;
using DataQL.Abstractions;
using DataQL.Ast.Model;
using DataQL.Cosmos.Translation;

namespace DataQL.Cosmos;

public sealed class CosmosQueryTranslator : IQueryProviderTranslator
{
    public string Provider => ProviderName.Cosmos;

    public ProviderCapabilities Capabilities { get; } = new()
    {
        Provider = ProviderName.Cosmos,
        Description = "Azure Cosmos DB document provider with feed-token paging, nested fields, and best-effort grouping.",
        Notes =
        [
            new CapabilityNote
            {
                Code = "Order.Optional",
                Severity = "info",
                Message = "order is not required on every query; it is required when a continuation token is supplied."
            },
            new CapabilityNote
            {
                Code = "Paging.FeedToken",
                Severity = "info",
                Message = "Non-group continuation uses Cosmos feed tokens. Do not change the request shape between pages."
            },
            new CapabilityNote
            {
                Code = "Group.NoContinuation",
                Severity = "restriction",
                Message = "Grouped queries return the full aggregate set: no continuation token, and limit is ignored."
            },
            new CapabilityNote
            {
                Code = "Group.OrderClientSide",
                Severity = "warning",
                Message = "When order is provided with group, results are sorted in-memory after aggregation."
            },
            new CapabilityNote
            {
                Code = "Count.ExtraQuery",
                Severity = "warning",
                Message = "includeCount runs a separate COUNT query; RUs are reported as _meta.countRequestCharge."
            },
            new CapabilityNote
            {
                Code = "Schema.BestEffort",
                Severity = "info",
                Message = "Container schema metadata is a minimal object stub; Cosmos is schemaless."
            }
        ],
        SupportedOperators = new HashSet<string>
        {
            "$eq", "$ne", "$gt", "$gte", "$lt", "$lte",
            "$in", "$nin",
            "$contains", "$startsWith", "$endsWith", "$regex",
            "$exists", "$isNull",
            "$containsAny", "$containsAll", "$size", "$isEmpty", "$any",
            "$and", "$or", "$not"
        },
        SupportsSelect = true,
        SupportsExclude = false,
        SupportsDistinct = false,
        SupportsGrouping = true,
        SupportsHaving = false,
        SupportsNestedFields = true,
        SupportedGroupOperations = new HashSet<string>
        {
            "count", "sum", "avg", "min", "max"
        }
    };

    public object Translate(QueryAst queryAst, QuerySource source)
    {
        if (!string.Equals(source.Provider, ProviderName.Cosmos, System.StringComparison.OrdinalIgnoreCase))
        {
            throw new System.ArgumentException($"Source provider '{source.Provider}' is not valid for '{ProviderName.Cosmos}'.", nameof(source));
        }

        if (string.IsNullOrWhiteSpace(source.Name))
        {
            throw new System.ArgumentException("Source name (container) is required for Cosmos translation.", nameof(source));
        }

        return new CosmosSqlTranslator().Translate(queryAst);
    }
}
