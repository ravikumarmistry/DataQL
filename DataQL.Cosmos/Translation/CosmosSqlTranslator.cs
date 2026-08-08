using System;
using System.Collections.Generic;
using System.Linq;
using DataQL.Ast.Model;

namespace DataQL.Cosmos.Translation;

public sealed class CosmosSqlTranslator
{
    public CosmosSqlTranslationResult Translate(QueryAst queryAst)
    {
        var parameters = new Dictionary<string, object?>();
        var parameterIndex = 0;

        if (queryAst.Group is null)
        {
            return TranslateNonGrouped(queryAst, parameters, ref parameterIndex);
        }

        return TranslateGrouped(queryAst, parameters, ref parameterIndex);
    }

    public CosmosSqlTranslationResult Translate(FilterExpression filter)
    {
        var parameters = new Dictionary<string, object?>();
        var parameterIndex = 0;
        var whereClause = BuildClause(filter, parameters, ref parameterIndex, alias: "c");

        return new CosmosSqlTranslationResult
        {
            Sql = $"SELECT * FROM c WHERE {whereClause}",
            Parameters = parameters
        };
    }

    private static CosmosSqlTranslationResult TranslateNonGrouped(
        QueryAst queryAst,
        IDictionary<string, object?> parameters,
        ref int parameterIndex)
    {
        var sql = "SELECT " + BuildSelectProjection(queryAst.Projection) + " FROM c";

        if (queryAst.Where is not null)
        {
            var whereClause = BuildClause(queryAst.Where, parameters, ref parameterIndex, alias: "c");
            sql += " WHERE " + whereClause;
        }

        if (queryAst.Order.Count > 0)
        {
            sql += " ORDER BY " + string.Join(", ", queryAst.Order.Select(o => $"c.{o.Field.Value} {(o.Direction == SortDirection.Asc ? "ASC" : "DESC")}"));
        }

        // Paging uses Cosmos feed MaxItemCount + continuation tokens (no OFFSET/LIMIT in SQL).

        return new CosmosSqlTranslationResult
        {
            Sql = sql,
            Parameters = new Dictionary<string, object?>(parameters)
        };
    }

    private static string BuildSelectProjection(ProjectionAst projection)
    {
        if (projection.Select.Count == 0)
        {
            return "*";
        }

        return string.Join(", ", projection.Select.Select(static field =>
        {
            var path = field.Value;
            var alias = path.Contains('.', StringComparison.Ordinal)
                ? path.Replace('.', '_')
                : path;
            return $"c.{path} AS {alias}";
        }));
    }

    private static CosmosSqlTranslationResult TranslateGrouped(
        QueryAst queryAst,
        IDictionary<string, object?> parameters,
        ref int parameterIndex)
    {
        var group = queryAst.Group!;
        if (group.Having is not null)
        {
            throw new NotSupportedException("Cosmos grouped translation does not support 'having'.");
        }

        var groupByFields = group.GroupBy.Select(f => "c." + f.Value).ToList();

        var projectionParts = new List<string>();
        foreach (var field in group.GroupBy)
        {
            var alias = BuildKeyAlias(field.Value);
            projectionParts.Add($"c.{field.Value} AS {alias}");
        }

        foreach (var metric in group.Metrics)
        {
            projectionParts.Add($"{BuildGroupMetricExpression(metric)} AS {metric.Alias}");
        }

        // Prefer non-VALUE projections: SELECT VALUE { ... aggregates ... } is rejected by Cosmos
        // ("Compositions of aggregates and other expressions are not allowed").
        var groupedSql = "SELECT " + string.Join(", ", projectionParts) + " FROM c";

        if (queryAst.Where is not null)
        {
            var whereClause = BuildClause(queryAst.Where, parameters, ref parameterIndex, alias: "c");
            groupedSql += " WHERE " + whereClause;
        }

        groupedSql += " GROUP BY " + string.Join(", ", groupByFields);

        // ORDER BY on grouped queries is unreliable across partitions; execution engine sorts
        // client-side. Paging uses feed MaxItemCount + continuation (no OFFSET/LIMIT).

        return new CosmosSqlTranslationResult
        {
            Sql = groupedSql,
            Parameters = new Dictionary<string, object?>(parameters),
            IsGrouped = true
        };
    }

    private static string BuildGroupMetricExpression(GroupMetricAst metric)
    {
        return metric.Operation switch
        {
            GroupMetricOperation.Count => "COUNT(1)",
            GroupMetricOperation.Sum => $"SUM(c.{metric.Field.Value})",
            GroupMetricOperation.Avg => $"AVG(c.{metric.Field.Value})",
            GroupMetricOperation.Min => $"MIN(c.{metric.Field.Value})",
            GroupMetricOperation.Max => $"MAX(c.{metric.Field.Value})",
            GroupMetricOperation.First => throw new NotSupportedException("Cosmos grouped translation does not support 'first' metric operation."),
            GroupMetricOperation.Last => throw new NotSupportedException("Cosmos grouped translation does not support 'last' metric operation."),
            _ => throw new NotSupportedException($"Unsupported group metric operation: {metric.Operation}")
        };
    }

    private static string BuildKeyAlias(string fieldPath)
    {
        return fieldPath.Replace('.', '_');
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
            NotFilter notNode => $"NOT ({BuildClause(notNode.Child, parameters, ref parameterIndex, alias)})",
            _ => throw new NotSupportedException($"Unsupported filter node: {node.GetType().Name}")
        };
    }

    private static string BuildLogical(
        LogicalFilter logical,
        IDictionary<string, object?> parameters,
        ref int parameterIndex,
        string alias)
    {
        var op = logical.Operator == FilterLogicalOperator.And ? "AND" : "OR";
        if (logical.Children.Count == 0)
        {
            return "1 = 1";
        }

        var clauses = new List<string>();
        foreach (var child in logical.Children)
        {
            clauses.Add($"({BuildClause(child, parameters, ref parameterIndex, alias)})");
        }

        return string.Join($" {op} ", clauses);
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

        var clauses = new List<string>();
        foreach (var operation in filter.Operations)
        {
            clauses.Add(BuildOperation(filter.Field.Value, operation, parameters, ref parameterIndex, alias));
        }

        return clauses.Count == 1 ? clauses[0] : "(" + string.Join(") AND (", clauses) + ")";
    }

    private static string BuildOperation(
        string fieldPath,
        FieldOperation operation,
        IDictionary<string, object?> parameters,
        ref int parameterIndex,
        string alias)
    {
        var field = $"{alias}.{fieldPath}";

        return operation switch
        {
            ScalarOperation scalar => BuildScalarOperation(field, scalar, parameters, ref parameterIndex),
            ListOperation list => BuildListOperation(field, list, parameters, ref parameterIndex),
            BooleanOperation boolean => BuildBooleanOperation(field, boolean),
            IntegerOperation integer => BuildIntegerOperation(field, integer, parameters, ref parameterIndex),
            AnyOperation any => BuildAnyOperation(field, any.Predicate, parameters, ref parameterIndex),
            _ => throw new NotSupportedException($"Unsupported operation type: {operation.GetType().Name}")
        };
    }

    private static string BuildScalarOperation(
        string field,
        ScalarOperation operation,
        IDictionary<string, object?> parameters,
        ref int parameterIndex)
    {
        var paramName = NextParameterName(ref parameterIndex);
        parameters[paramName] = ExtractScalar(operation.Value);

        var sqlOp = operation.Operator switch
        {
            FieldOperator.Eq => "=",
            FieldOperator.Ne => "!=",
            FieldOperator.Gt => ">",
            FieldOperator.Gte => ">=",
            FieldOperator.Lt => "<",
            FieldOperator.Lte => "<=",
            FieldOperator.Contains => null,
            FieldOperator.StartsWith => null,
            FieldOperator.EndsWith => null,
            FieldOperator.Regex => null,
            _ => throw new NotSupportedException($"Unsupported scalar operator: {operation.Operator}")
        };

        return operation.Operator switch
        {
            FieldOperator.Contains => $"CONTAINS({field}, {paramName})",
            FieldOperator.StartsWith => $"STARTSWITH({field}, {paramName})",
            FieldOperator.EndsWith => $"ENDSWITH({field}, {paramName})",
            FieldOperator.Regex => $"RegexMatch({field}, {paramName})",
            _ => $"{field} {sqlOp} {paramName}"
        };
    }

    private static string BuildListOperation(
        string field,
        ListOperation operation,
        IDictionary<string, object?> parameters,
        ref int parameterIndex)
    {
        var parameterNames = new List<string>();
        foreach (var value in operation.Values)
        {
            var name = NextParameterName(ref parameterIndex);
            parameterNames.Add(name);
            parameters[name] = ExtractScalar(value);
        }

        return operation.Operator switch
        {
            FieldOperator.In => $"{field} IN ({string.Join(", ", parameterNames)})",
            FieldOperator.Nin => $"{field} NOT IN ({string.Join(", ", parameterNames)})",
            FieldOperator.ContainsAny => "(" + string.Join(" OR ", parameterNames.Select(p => $"ARRAY_CONTAINS({field}, {p})")) + ")",
            FieldOperator.ContainsAll => "(" + string.Join(" AND ", parameterNames.Select(p => $"ARRAY_CONTAINS({field}, {p})")) + ")",
            _ => throw new NotSupportedException($"Unsupported list operator: {operation.Operator}")
        };
    }

    private static string BuildBooleanOperation(string field, BooleanOperation operation)
    {
        return operation.Operator switch
        {
            FieldOperator.Exists => operation.Value
                ? $"IS_DEFINED({field})"
                : $"NOT IS_DEFINED({field})",
            FieldOperator.IsNull => operation.Value
                ? $"IS_NULL({field})"
                : $"NOT IS_NULL({field})",
            FieldOperator.IsEmpty => operation.Value
                ? $"ARRAY_LENGTH({field}) = 0"
                : $"ARRAY_LENGTH({field}) > 0",
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

        var paramName = NextParameterName(ref parameterIndex);
        parameters[paramName] = operation.Value;
        return $"ARRAY_LENGTH({field}) = {paramName}";
    }

    private static string NextParameterName(ref int parameterIndex)
    {
        var name = $"@p{parameterIndex}";
        parameterIndex++;
        return name;
    }

    private static string BuildAnyOperation(
        string field,
        FilterExpression predicate,
        IDictionary<string, object?> parameters,
        ref int parameterIndex)
    {
        var alias = "a" + parameterIndex;
        var predicateClause = BuildClause(predicate, parameters, ref parameterIndex, alias);
        return $"EXISTS(SELECT VALUE {alias} FROM {alias} IN {field} WHERE {predicateClause})";
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
