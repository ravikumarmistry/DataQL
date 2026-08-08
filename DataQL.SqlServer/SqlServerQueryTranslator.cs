using DataQL.Abstractions;
using DataQL.Ast.Model;
using DataQL.SqlServer.Translation;

namespace DataQL.SqlServer;

public sealed class SqlServerQueryTranslator : IQueryProviderTranslator
{
    public string Provider => ProviderName.SqlServer;

    public ProviderCapabilities Capabilities { get; } = new()
    {
        Provider = ProviderName.SqlServer,
        Description = "SQL Server relational provider with seek paging, grouping, having, distinct, nested fields, and includeCount.",
        Notes =
        [
            new CapabilityNote
            {
                Code = "Order.Required",
                Severity = "restriction",
                Message = "order is required on every query for deterministic paging and seek continuation."
            },
            new CapabilityNote
            {
                Code = "Paging.Seek",
                Severity = "info",
                Message = "Continuation uses keyset seek tokens. Do not change the request shape between pages."
            }
        ],
        SupportedOperators = new HashSet<string>
        {
            "$eq", "$ne", "$gt", "$gte", "$lt", "$lte",
            "$in", "$nin",
            "$contains", "$startsWith", "$endsWith",
            "$exists", "$isNull",
            "$containsAny", "$containsAll", "$size", "$isEmpty", "$any",
            "$and", "$or", "$not"
        },
        SupportsSelect = true,
        SupportsExclude = true,
        SupportsGrouping = true,
        SupportsHaving = true,
        SupportsNestedFields = true,
        SupportsDistinct = true,
        SupportedGroupOperations = new HashSet<string>
        {
            "count", "sum", "avg", "min", "max"
        }
    };

    public object Translate(QueryAst queryAst, QuerySource source)
    {
        if (!string.Equals(source.Provider, ProviderName.SqlServer, System.StringComparison.OrdinalIgnoreCase))
        {
            throw new System.ArgumentException($"Source provider '{source.Provider}' is not valid for '{ProviderName.SqlServer}'.", nameof(source));
        }

        if (string.IsNullOrWhiteSpace(source.Name))
        {
            throw new System.ArgumentException("Source name (table) is required for SQL Server translation.", nameof(source));
        }

        return new SqlServerSqlTranslator().Translate(queryAst, source.Name);
    }
}
