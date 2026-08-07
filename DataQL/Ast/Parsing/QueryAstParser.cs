using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DataQL.Ast.Model;
using DataQL.Contracts;

namespace DataQL.Ast.Parsing;

public sealed class QueryAstParser
{
    public QueryAst Parse(QueryRequest request)
    {
        var where = ParseWhere(request.Where);
        var projection = new ProjectionAst(
            request.Select.Select(p => new FieldPath(p)).ToList(),
            request.Exclude.Select(p => new FieldPath(p)).ToList(),
            request.Distinct.Select(p => new FieldPath(p)).ToList());

        var order = request.Order
            .Select(o => new SortField(new FieldPath(o.Field), ParseSortDirection(o.Direction)))
            .ToList();

        var pagination = new PaginationAst(
            request.Limit,
            request.ContinuationToken,
            request.IncludeCount,
            RequiresDeterministicOrder: request.Limit.HasValue);

        var group = ParseGroup(request.Group);

        return new QueryAst(where, projection, order, pagination, group);
    }

    private static FilterExpression? ParseWhere(JsonElement? where)
    {
        return ParseFilter(where, "where");
    }

    private static FilterExpression? ParseFilter(JsonElement? filter, string path)
    {
        if (filter is null || filter.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (filter.Value.ValueKind != JsonValueKind.Object)
        {
            throw new AstParseException(path, path + " must be an object.");
        }

        return ParseFilterObject(filter.Value, path);
    }

    private static FilterExpression ParseFilterObject(JsonElement obj, string path)
    {
        var children = new List<FilterExpression>();

        foreach (var property in obj.EnumerateObject())
        {
            if (property.NameEquals("$and"))
            {
                children.Add(ParseLogicalArray(property.Value, path + ".$and", FilterLogicalOperator.And));
                continue;
            }

            if (property.NameEquals("$or"))
            {
                children.Add(ParseLogicalArray(property.Value, path + ".$or", FilterLogicalOperator.Or));
                continue;
            }

            if (property.NameEquals("$not"))
            {
                if (property.Value.ValueKind != JsonValueKind.Object)
                {
                    throw new AstParseException(path + ".$not", "$not must be an object.");
                }

                children.Add(new NotFilter(ParseFilterObject(property.Value, path + ".$not")));
                continue;
            }

            children.Add(ParseFieldFilter(property, path));
        }

        if (children.Count == 0)
        {
            return new LogicalFilter(FilterLogicalOperator.And, []);
        }

        if (children.Count == 1)
        {
            return children[0];
        }

        return new LogicalFilter(FilterLogicalOperator.And, children);
    }

    private static FilterExpression ParseLogicalArray(JsonElement value, string path, FilterLogicalOperator op)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new AstParseException(path, "logical operator must be an array.");
        }

        var nodes = new List<FilterExpression>();
        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new AstParseException(path + $"[{index}]", "logical array item must be an object.");
            }

            nodes.Add(ParseFilterObject(item, path + $"[{index}]"));
            index++;
        }

        return new LogicalFilter(op, nodes);
    }

    private static FilterExpression ParseFieldFilter(JsonProperty property, string path)
    {
        var field = new FieldPath(property.Name);

        if (property.Value.ValueKind != JsonValueKind.Object)
        {
            return new FieldFilter(field, [new ScalarOperation(FieldOperator.Eq, ParseValue(property.Value))]);
        }

        var operations = new List<FieldOperation>();
        foreach (var opProperty in property.Value.EnumerateObject())
        {
            operations.Add(ParseFieldOperation(opProperty, path + "." + property.Name));
        }

        return new FieldFilter(field, operations);
    }

    private static FieldOperation ParseFieldOperation(JsonProperty property, string path)
    {
        var op = property.Name;
        var opPath = path + "." + op;

        return op switch
        {
            "$eq" => new ScalarOperation(FieldOperator.Eq, ParseValue(property.Value)),
            "$ne" => new ScalarOperation(FieldOperator.Ne, ParseValue(property.Value)),
            "$gt" => new ScalarOperation(FieldOperator.Gt, ParseValue(property.Value)),
            "$gte" => new ScalarOperation(FieldOperator.Gte, ParseValue(property.Value)),
            "$lt" => new ScalarOperation(FieldOperator.Lt, ParseValue(property.Value)),
            "$lte" => new ScalarOperation(FieldOperator.Lte, ParseValue(property.Value)),
            "$in" => new ListOperation(FieldOperator.In, ParseArrayValues(property.Value, opPath)),
            "$nin" => new ListOperation(FieldOperator.Nin, ParseArrayValues(property.Value, opPath)),
            "$contains" => new ScalarOperation(FieldOperator.Contains, ParseValue(property.Value)),
            "$startsWith" => new ScalarOperation(FieldOperator.StartsWith, ParseValue(property.Value)),
            "$endsWith" => new ScalarOperation(FieldOperator.EndsWith, ParseValue(property.Value)),
            "$regex" => new ScalarOperation(FieldOperator.Regex, ParseValue(property.Value)),
            "$exists" => new BooleanOperation(FieldOperator.Exists, ParseBoolean(property.Value, opPath)),
            "$isNull" => new BooleanOperation(FieldOperator.IsNull, ParseBoolean(property.Value, opPath)),
            "$containsAny" => new ListOperation(FieldOperator.ContainsAny, ParseArrayValues(property.Value, opPath)),
            "$containsAll" => new ListOperation(FieldOperator.ContainsAll, ParseArrayValues(property.Value, opPath)),
            "$size" => new IntegerOperation(FieldOperator.Size, ParseInteger(property.Value, opPath)),
            "$isEmpty" => new BooleanOperation(FieldOperator.IsEmpty, ParseBoolean(property.Value, opPath)),
            "$any" => new AnyOperation(ParseAnyPredicate(property.Value, opPath)),
            _ => throw new AstParseException(opPath, $"Unknown operator '{op}'.")
        };
    }

    private static FilterExpression ParseAnyPredicate(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new AstParseException(path, "$any must be an object predicate.");
        }

        return ParseFilterObject(value, path);
    }

    private static IReadOnlyList<AstValue> ParseArrayValues(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new AstParseException(path, "operator requires an array value.");
        }

        var values = new List<AstValue>();
        foreach (var item in value.EnumerateArray())
        {
            values.Add(ParseValue(item));
        }

        return values;
    }

    private static bool ParseBoolean(JsonElement value, string path)
    {
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new AstParseException(path, "operator requires a boolean value.");
        }

        return value.GetBoolean();
    }

    private static int ParseInteger(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var result))
        {
            throw new AstParseException(path, "operator requires an integer value.");
        }

        return result;
    }

    private static AstValue ParseValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Null => new ScalarValue(null),
            JsonValueKind.True => new ScalarValue(true),
            JsonValueKind.False => new ScalarValue(false),
            JsonValueKind.String => new ScalarValue(value.GetString()),
            JsonValueKind.Number => ParseNumber(value),
            JsonValueKind.Array => new ArrayValue(value.EnumerateArray().Select(ParseValue).ToList()),
            JsonValueKind.Object => new ObjectValue(value.EnumerateObject().ToDictionary(p => p.Name, p => ParseValue(p.Value))),
            _ => throw new AstParseException("value", "Unsupported value kind.")
        };
    }

    private static AstValue ParseNumber(JsonElement value)
    {
        if (value.TryGetInt64(out var longValue))
        {
            return new ScalarValue(longValue);
        }

        if (value.TryGetDouble(out var doubleValue))
        {
            return new ScalarValue(doubleValue);
        }

        return new ScalarValue(value.GetDecimal());
    }

    private static GroupAst? ParseGroup(GroupRequest? group)
    {
        if (group is null)
        {
            return null;
        }

        var groupBy = new List<FieldPath>(group.GroupBy.Count);
        for (var i = 0; i < group.GroupBy.Count; i++)
        {
            var field = group.GroupBy[i] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(field))
            {
                throw new AstParseException($"group.groupBy[{i}]", "groupBy field is required.");
            }

            groupBy.Add(new FieldPath(field));
        }

        var metrics = new List<GroupMetricAst>(group.Metrics.Count);
        for (var i = 0; i < group.Metrics.Count; i++)
        {
            var metric = group.Metrics[i];
            metrics.Add(ParseGroupMetric(metric, i));
        }

        var having = ParseFilter(group.Having, "group.having");

        return new GroupAst(groupBy, metrics, having);
    }

    private static GroupMetricAst ParseGroupMetric(GroupMetricRequest metric, int index)
    {
        var path = $"group.metrics[{index}]";

        if (metric is null)
        {
            throw new AstParseException(path, "metric is required.");
        }

        var field = metric.Field ?? string.Empty;
        if (string.IsNullOrWhiteSpace(field))
        {
            throw new AstParseException(path + ".field", "metric field is required.");
        }

        var alias = metric.Alias ?? string.Empty;
        if (string.IsNullOrWhiteSpace(alias))
        {
            throw new AstParseException(path + ".alias", "metric alias is required.");
        }

        var operation = ParseGroupMetricOperation(metric.Operation, path + ".operation");

        if (operation != GroupMetricOperation.Count && field == "*")
        {
            throw new AstParseException(path + ".field", "Only count operation supports '*' field.");
        }

        return new GroupMetricAst(new FieldPath(field), operation, alias);
    }

    private static GroupMetricOperation ParseGroupMetricOperation(string? operation, string path)
    {
        return (operation ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "count" => GroupMetricOperation.Count,
            "sum" => GroupMetricOperation.Sum,
            "avg" => GroupMetricOperation.Avg,
            "min" => GroupMetricOperation.Min,
            "max" => GroupMetricOperation.Max,
            "first" => GroupMetricOperation.First,
            "last" => GroupMetricOperation.Last,
            _ => throw new AstParseException(path, $"Unsupported metric operation '{operation}'.")
        };
    }

    private static string ParseString(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new AstParseException(path, "value must be a string.");
        }

        return value.GetString() ?? string.Empty;
    }

    private static SortDirection ParseSortDirection(string direction)
    {
        return direction.Trim().ToLowerInvariant() switch
        {
            "asc" => SortDirection.Asc,
            "desc" => SortDirection.Desc,
            _ => throw new AstParseException("order.direction", "order direction must be 'asc' or 'desc'.")
        };
    }
}
