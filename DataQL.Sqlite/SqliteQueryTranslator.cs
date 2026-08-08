using DataQL.Abstractions;
using DataQL.Ast.Model;
using DataQL.Sqlite.Translation;

namespace DataQL.Sqlite;

public sealed class SqliteQueryTranslator : IQueryProviderTranslator
{
    public string Provider => ProviderName.Sqlite;

    public ProviderCapabilities Capabilities { get; } = new()
    {
        Provider = ProviderName.Sqlite,
        Description = "SQLite relational provider with seek paging, grouping, having, distinct, and includeCount.",
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
            "$and", "$or", "$not"
        },
        SupportsSelect = true,
        SupportsExclude = true,
        SupportsGrouping = true,
        SupportsHaving = true,
        SupportsNestedFields = false,
        SupportsDistinct = true,
        SupportedGroupOperations = new HashSet<string>
        {
            "count", "sum", "avg", "min", "max"
        }
    };

    public object Translate(QueryAst queryAst, QuerySource source)
    {
        if (!string.Equals(source.Provider, Provider, System.StringComparison.OrdinalIgnoreCase))
        {
            throw new System.ArgumentException($"Source provider '{source.Provider}' is not valid for '{Provider}'.", nameof(source));
        }

        if (string.IsNullOrWhiteSpace(source.Name))
        {
            throw new System.ArgumentException("Source name (table) is required for SQLite translation.", nameof(source));
        }

        var tableName = ResolveSqliteTableName(source);

        return new SqliteSqlTranslator().Translate(queryAst, tableName);
    }

    private static string ResolveSqliteTableName(QuerySource source)
    {
        var split = source.Name.Split('.', 2, System.StringSplitOptions.TrimEntries | System.StringSplitOptions.RemoveEmptyEntries);
        if (split.Length == 2)
        {
            return split[0] + "." + split[1];
        }

        return source.Name;
    }
}
