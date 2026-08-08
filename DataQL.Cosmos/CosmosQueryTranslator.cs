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
