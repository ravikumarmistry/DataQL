using System;
using System.Collections.Generic;
using System.Linq;
using DataQL.Ast.Model;

namespace DataQL.SqlServer.Translation;

public sealed class SqlServerSqlTranslator
{
    public SqlServerSqlTranslationResult Translate(QueryAst queryAst, string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        var parameters = new Dictionary<string, object?>();
        var parameterIndex = 0;

        var from = " FROM " + QuoteQualifiedIdentifier(tableName) + " AS [t]";

        if (queryAst.Group is null)
        {
            var useDistinct = queryAst.Projection.Distinct.Count > 0;
            var projection = useDistinct
                ? BuildDistinctProjection(queryAst.Projection.Distinct, "[t]")
                : BuildProjection(queryAst.Projection, "[t]");
            var distinctPrefix = useDistinct ? "DISTINCT " : string.Empty;
            var top = queryAst.Pagination.Limit is > 0
                ? "TOP (" + queryAst.Pagination.Limit.Value + ") "
                : string.Empty;
            var sql = "SELECT " + distinctPrefix + top + projection + from;

            if (queryAst.Where is not null)
            {
                var where = BuildClause(queryAst.Where, parameters, ref parameterIndex, "[t]");
                sql += " WHERE " + where;
            }

            if (queryAst.Order.Count > 0)
            {
                sql += " ORDER BY " + string.Join(", ", queryAst.Order.Select(o => BuildOrder(o, "[t]")));
            }

            return new SqlServerSqlTranslationResult
            {
                Sql = sql,
                Parameters = parameters
            };
        }

        var group = queryAst.Group;

        var selectParts = new List<string>();
        var groupByParts = new List<string>();

        foreach (var key in group.GroupBy)
        {
            var keyExpr = BuildFieldReference(key.Value, "[t]");
            var alias = "[" + BuildGroupKeyAlias(key.Value) + "]";
            selectParts.Add($"{keyExpr} AS {alias}");
            groupByParts.Add(keyExpr);
        }

        foreach (var metric in group.Metrics)
        {
            selectParts.Add(BuildMetric(metric, "[t]"));
        }

        var groupedSql = "SELECT " + string.Join(", ", selectParts) + from;

        if (queryAst.Where is not null)
        {
            var where = BuildClause(queryAst.Where, parameters, ref parameterIndex, "[t]");
            groupedSql += " WHERE " + where;
        }

        groupedSql += " GROUP BY " + string.Join(", ", groupByParts);

        if (group.Having is not null)
        {
            var havingFields = BuildHavingFieldMap(group, "[t]");
            var having = BuildHavingClause(group.Having, parameters, ref parameterIndex, havingFields);
            groupedSql += " HAVING " + having;
        }

        var sqlWithPost = groupedSql;
        if (queryAst.Order.Count > 0 || queryAst.Pagination.Limit is > 0)
        {
            sqlWithPost = "SELECT * FROM (" + groupedSql + ") AS [g]";

            if (queryAst.Order.Count > 0)
            {
                sqlWithPost += " ORDER BY " + string.Join(", ", queryAst.Order.Select(o => BuildOrder(o, "[g]")));
            }

            if (queryAst.Pagination.Limit is > 0)
            {
                if (queryAst.Order.Count == 0)
                {
                    sqlWithPost += " ORDER BY (SELECT 1)";
                }

                sqlWithPost += $" OFFSET 0 ROWS FETCH NEXT {queryAst.Pagination.Limit.Value} ROWS ONLY";
            }
        }

        return new SqlServerSqlTranslationResult
        {
            Sql = sqlWithPost,
            Parameters = parameters
        };
    }

    private static string BuildMetric(GroupMetricAst metric, string alias)
    {
        var metricAlias = "[" + metric.Alias + "]";
        return BuildMetricExpression(metric, alias) + " AS " + metricAlias;
    }

    private static string BuildMetricExpression(GroupMetricAst metric, string alias)
    {
        var fieldExpression = BuildFieldReference(metric.Field.Value, alias);

        return metric.Operation switch
        {
            GroupMetricOperation.Count => "COUNT(1)",
            GroupMetricOperation.Sum => $"SUM({fieldExpression})",
            GroupMetricOperation.Avg => $"AVG(CAST({fieldExpression} AS float))",
            GroupMetricOperation.Min => $"MIN({fieldExpression})",
            GroupMetricOperation.Max => $"MAX({fieldExpression})",
            GroupMetricOperation.First => throw new NotSupportedException("SqlServer grouped translation does not support 'first' metric operation."),
            GroupMetricOperation.Last => throw new NotSupportedException("SqlServer grouped translation does not support 'last' metric operation."),
            _ => throw new NotSupportedException($"Unsupported metric operation: {metric.Operation}")
        };
    }

    private static IReadOnlyDictionary<string, string> BuildHavingFieldMap(GroupAst group, string tableAlias)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in group.GroupBy)
        {
            map[key.Value] = BuildFieldReference(key.Value, tableAlias);
        }

        foreach (var metric in group.Metrics)
        {
            map[metric.Alias] = BuildMetricExpression(metric, tableAlias);
        }

        return map;
    }

    private static string BuildHavingClause(
        FilterExpression node,
        IDictionary<string, object?> parameters,
        ref int parameterIndex,
        IReadOnlyDictionary<string, string> fieldMap)
    {
        return node switch
        {
            FieldFilter fieldFilter => BuildHavingFieldFilter(fieldFilter, parameters, ref parameterIndex, fieldMap),
            LogicalFilter logical => BuildHavingLogical(logical, parameters, ref parameterIndex, fieldMap),
            NotFilter notNode => "NOT (" + BuildHavingClause(notNode.Child, parameters, ref parameterIndex, fieldMap) + ")",
            _ => throw new NotSupportedException($"Unsupported filter node: {node.GetType().Name}")
        };
    }

    private static string BuildHavingLogical(
        LogicalFilter logical,
        IDictionary<string, object?> parameters,
        ref int parameterIndex,
        IReadOnlyDictionary<string, string> fieldMap)
    {
        if (logical.Children.Count == 0)
        {
            return "1 = 1";
        }

        var op = logical.Operator == FilterLogicalOperator.And ? "AND" : "OR";
        var parts = new List<string>();
        foreach (var child in logical.Children)
        {
            parts.Add("(" + BuildHavingClause(child, parameters, ref parameterIndex, fieldMap) + ")");
        }

        return string.Join(" " + op + " ", parts);
    }

    private static string BuildHavingFieldFilter(
        FieldFilter filter,
        IDictionary<string, object?> parameters,
        ref int parameterIndex,
        IReadOnlyDictionary<string, string> fieldMap)
    {
        if (filter.Operations.Count == 0)
        {
            return "1 = 1";
        }

        if (!fieldMap.TryGetValue(filter.Field.Value, out var field))
        {
            throw new NotSupportedException(
                $"Having field '{filter.Field.Value}' does not match a groupBy field or metric alias.");
        }

        var parts = new List<string>();
        foreach (var operation in filter.Operations)
        {
            parts.Add(BuildOperation(field, operation, parameters, ref parameterIndex, alias: string.Empty));
        }

        return parts.Count == 1 ? parts[0] : "(" + string.Join(") AND (", parts) + ")";
    }

    private static string BuildClause(
        FilterExpression node,
        IDictionary<string, object?> parameters,
        ref int parameterIndex,
        string alias)
    {
        return node switch
        {
            FieldFilter fieldFilter => BuildFieldFilter(fieldFilter, parameters, ref parameterIndex, alias),
            LogicalFilter logical => BuildLogical(logical, parameters, ref parameterIndex, alias),
            NotFilter notNode => "NOT (" + BuildClause(notNode.Child, parameters, ref parameterIndex, alias) + ")",
            _ => throw new NotSupportedException($"Unsupported filter node: {node.GetType().Name}")
        };
    }

    private static string BuildLogical(
        LogicalFilter logical,
        IDictionary<string, object?> parameters,
        ref int parameterIndex,
        string alias)
    {
        if (logical.Children.Count == 0)
        {
            return "1 = 1";
        }

        var op = logical.Operator == FilterLogicalOperator.And ? "AND" : "OR";
        var parts = new List<string>();
        foreach (var child in logical.Children)
        {
            parts.Add("(" + BuildClause(child, parameters, ref parameterIndex, alias) + ")");
        }

        return string.Join(" " + op + " ", parts);
    }

    private static string BuildFieldFilter(
        FieldFilter filter,
        IDictionary<string, object?> parameters,
        ref int parameterIndex,
        string alias)
    {
        if (filter.Operations.Count == 0)
        {
            return "1 = 1";
        }

        var field = BuildFieldReference(filter.Field.Value, alias);
        var parts = new List<string>();
        foreach (var operation in filter.Operations)
        {
            parts.Add(BuildOperation(field, operation, parameters, ref parameterIndex, alias));
        }

        return parts.Count == 1 ? parts[0] : "(" + string.Join(") AND (", parts) + ")";
    }

    private static string BuildOperation(
        string field,
        FieldOperation operation,
        IDictionary<string, object?> parameters,
        ref int parameterIndex,
        string alias)
    {
        return operation switch
        {
            ScalarOperation scalar => BuildScalarOperation(field, scalar, parameters, ref parameterIndex),
            ListOperation list => BuildListOperation(field, list, parameters, ref parameterIndex),
            BooleanOperation boolean => BuildBooleanOperation(field, boolean),
            IntegerOperation integer => BuildIntegerOperation(field, integer, parameters, ref parameterIndex),
            AnyOperation any => BuildAnyOperation(field, any.Predicate, parameters, ref parameterIndex, alias),
            _ => throw new NotSupportedException($"Unsupported operation type: {operation.GetType().Name}")
        };
    }

    private static string BuildScalarOperation(
        string field,
        ScalarOperation operation,
        IDictionary<string, object?> parameters,
        ref int parameterIndex)
    {
        var param = NextParam(ref parameterIndex);
        parameters[param] = ExtractScalar(operation.Value);

        return operation.Operator switch
        {
            FieldOperator.Eq => field + " = " + param,
            FieldOperator.Ne => field + " <> " + param,
            FieldOperator.Gt => field + " > " + param,
            FieldOperator.Gte => field + " >= " + param,
            FieldOperator.Lt => field + " < " + param,
            FieldOperator.Lte => field + " <= " + param,
            FieldOperator.Contains => field + " LIKE '%' + " + param + " + '%'",
            FieldOperator.StartsWith => field + " LIKE " + param + " + '%'",
            FieldOperator.EndsWith => field + " LIKE '%' + " + param,
            FieldOperator.Regex => throw new NotSupportedException("SqlServer translation does not support regex."),
            _ => throw new NotSupportedException($"Unsupported scalar operator: {operation.Operator}")
        };
    }

    private static string BuildListOperation(
        string field,
        ListOperation operation,
        IDictionary<string, object?> parameters,
        ref int parameterIndex)
    {
        var names = new List<string>();
        foreach (var value in operation.Values)
        {
            var p = NextParam(ref parameterIndex);
            names.Add(p);
            parameters[p] = ExtractScalar(value);
        }

        return operation.Operator switch
        {
            FieldOperator.In => field + " IN (" + string.Join(", ", names) + ")",
            FieldOperator.Nin => field + " NOT IN (" + string.Join(", ", names) + ")",
            FieldOperator.ContainsAny => BuildContainsJson(field, names, requireAll: false),
            FieldOperator.ContainsAll => BuildContainsJson(field, names, requireAll: true),
            _ => throw new NotSupportedException($"Unsupported list operator: {operation.Operator}")
        };
    }

    private static string BuildContainsJson(string field, IReadOnlyList<string> names, bool requireAll)
    {
        if (names.Count == 0)
        {
            return requireAll ? "1 = 1" : "1 = 0";
        }

        var op = requireAll ? "AND" : "OR";
        var checks = names.Select(name => "EXISTS (SELECT 1 FROM OPENJSON(" + field + ") WHERE [value] = " + name + ")");
        return "(" + string.Join(" " + op + " ", checks) + ")";
    }

    private static string BuildBooleanOperation(string field, BooleanOperation operation)
    {
        return operation.Operator switch
        {
            FieldOperator.Exists => operation.Value
                ? field + " IS NOT NULL"
                : field + " IS NULL",
            FieldOperator.IsNull => operation.Value
                ? field + " IS NULL"
                : field + " IS NOT NULL",
            FieldOperator.IsEmpty => operation.Value
                ? "NOT EXISTS (SELECT 1 FROM OPENJSON(" + field + "))"
                : "EXISTS (SELECT 1 FROM OPENJSON(" + field + "))",
            _ => throw new NotSupportedException($"Unsupported boolean operator: {operation.Operator}")
        };
    }

    private static string BuildIntegerOperation(
        string field,
        IntegerOperation operation,
        IDictionary<string, object?> parameters,
        ref int parameterIndex)
    {
        if (operation.Operator is not FieldOperator.Size)
        {
            throw new NotSupportedException($"Unsupported integer operator: {operation.Operator}");
        }

        var p = NextParam(ref parameterIndex);
        parameters[p] = operation.Value;
        return "(SELECT COUNT(1) FROM OPENJSON(" + field + ")) = " + p;
    }

    private static string BuildAnyOperation(
        string field,
        FilterExpression predicate,
        IDictionary<string, object?> parameters,
        ref int parameterIndex,
        string alias)
    {
        var itemAlias = "[a" + parameterIndex + "]";
        var predicateClause = BuildClause(predicate, parameters, ref parameterIndex, itemAlias);
        return "EXISTS (SELECT 1 FROM OPENJSON(" + field + ") WITH ([value] NVARCHAR(MAX) '$' AS JSON) AS " + itemAlias + " WHERE " + predicateClause + ")";
    }

    private static string BuildFieldReference(string dottedPath, string alias)
    {
        var parts = dottedPath.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            throw new ArgumentException("Field path cannot be empty.", nameof(dottedPath));
        }

        if (parts.Length == 1)
        {
            return alias + "." + QuoteIdentifier(parts[0]);
        }

        var first = alias + "." + QuoteIdentifier(parts[0]);
        var jsonPath = "$" + string.Concat(parts.Skip(1).Select(p => "." + p));
        return "JSON_VALUE(" + first + ", '" + jsonPath + "')";
    }

    private static string BuildOrder(SortField sort, string alias)
    {
        var direction = sort.Direction == SortDirection.Asc ? "ASC" : "DESC";
        return BuildFieldReference(sort.Field.Value, alias) + " " + direction;
    }

    private static string BuildGroupKeyAlias(string fieldPath)
    {
        return fieldPath.Replace('.', '_');
    }

    private static string BuildDistinctProjection(IReadOnlyList<FieldPath> distinctFields, string alias)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var selectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var parts = new List<string>();

        foreach (var field in distinctFields)
        {
            var path = field.Value;
            if (string.IsNullOrWhiteSpace(path) || !selectedPaths.Add(path))
            {
                continue;
            }

            var expression = BuildFieldReference(path, alias);
            var aliasName = BuildUniqueProjectionAlias(path, aliases);
            parts.Add(expression + " AS " + QuoteIdentifier(aliasName));
        }

        if (parts.Count == 0)
        {
            throw new ArgumentException("Distinct projection cannot be empty.", nameof(distinctFields));
        }

        return string.Join(", ", parts);
    }

    private static string BuildProjection(ProjectionAst projection, string alias)
    {
        if (projection.Select.Count == 0)
        {
            if (projection.Exclude.Count > 0)
            {
                throw new NotSupportedException("SqlServer translation requires 'select' when using 'exclude'.");
            }

            return alias + ".*";
        }

        var excluded = new HashSet<string>(projection.Exclude.Select(f => f.Value), StringComparer.OrdinalIgnoreCase);
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var selectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var parts = new List<string>();

        foreach (var field in projection.Select)
        {
            var path = field.Value;
            if (excluded.Contains(path) || !selectedPaths.Add(path))
            {
                continue;
            }

            var expression = BuildFieldReference(path, alias);
            var aliasName = BuildUniqueProjectionAlias(path, aliases);
            parts.Add(expression + " AS " + QuoteIdentifier(aliasName));
        }

        if (parts.Count == 0)
        {
            throw new ArgumentException("Projection cannot be empty after applying exclude.", nameof(projection));
        }

        return string.Join(", ", parts);
    }

    private static string BuildUniqueProjectionAlias(string fieldPath, ISet<string> aliases)
    {
        var baseAlias = fieldPath.Replace('.', '_');
        if (aliases.Add(baseAlias))
        {
            return baseAlias;
        }

        var index = 2;
        while (true)
        {
            var candidate = baseAlias + "_" + index;
            if (aliases.Add(candidate))
            {
                return candidate;
            }

            index++;
        }
    }

    private static string QuoteIdentifier(string identifier)
    {
        return "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]";
    }

    private static string QuoteQualifiedIdentifier(string identifier)
    {
        var parts = identifier.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join('.', parts.Select(QuoteIdentifier));
    }

    private static string NextParam(ref int parameterIndex)
    {
        var name = "@p" + parameterIndex;
        parameterIndex++;
        return name;
    }

    private static object? ExtractScalar(AstValue value)
    {
        return value switch
        {
            ScalarValue scalar => scalar.Value,
            _ => throw new NotSupportedException("Only scalar values are supported for this operation.")
        };
    }
 }
