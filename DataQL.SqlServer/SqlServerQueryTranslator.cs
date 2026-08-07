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
