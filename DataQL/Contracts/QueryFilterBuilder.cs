using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DataQL.Contracts;

public sealed class QueryFilter
{
    private readonly JsonNode _node;

    internal QueryFilter(JsonNode node)
    {
        _node = node ?? throw new ArgumentNullException(nameof(node));
    }

    internal JsonNode ToJsonNode()
    {
        return _node.DeepClone();
    }

    public JsonElement ToJsonElement()
    {
        return JsonSerializer.SerializeToElement(_node);
    }
}

public static class QueryFilterBuilder
{
    public static FieldFilterBuilder Field(string field)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            throw new ArgumentException("Field is required.", nameof(field));
        }

        return new FieldFilterBuilder(field);
    }

    public static QueryFilter And(params QueryFilter[] filters)
    {
        return BuildLogical("$and", filters);
    }

    public static QueryFilter Or(params QueryFilter[] filters)
    {
        return BuildLogical("$or", filters);
    }

    public static QueryFilter Not(QueryFilter filter)
    {
        if (filter is null)
        {
            throw new ArgumentNullException(nameof(filter));
        }

        return new QueryFilter(new JsonObject
        {
            ["$not"] = filter.ToJsonNode()
        });
    }

    public static JsonElement MergeAnd(JsonElement? existingWhere, QueryFilter guard)
    {
        if (guard is null)
        {
            throw new ArgumentNullException(nameof(guard));
        }

        if (existingWhere is null
            || existingWhere.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return guard.ToJsonElement();
        }

        var existingNode = JsonNode.Parse(existingWhere.Value.GetRawText())
            ?? throw new ArgumentException("Existing where filter is invalid.", nameof(existingWhere));

        var merged = And(new QueryFilter(existingNode), guard);
        return merged.ToJsonElement();
    }

    private static QueryFilter BuildLogical(string op, params QueryFilter[] filters)
    {
        if (filters is null)
        {
            throw new ArgumentNullException(nameof(filters));
        }

        if (filters.Length == 0)
        {
            throw new ArgumentException("At least one filter is required.", nameof(filters));
        }

        if (filters.Any(static f => f is null))
        {
            throw new ArgumentException("Filters cannot contain null values.", nameof(filters));
        }

        var array = new JsonArray(filters.Select(f => f.ToJsonNode()).ToArray());
        return new QueryFilter(new JsonObject
        {
            [op] = array
        });
    }
}

public sealed class FieldFilterBuilder
{
    private readonly string _field;

    internal FieldFilterBuilder(string field)
    {
        _field = field;
    }

    public QueryFilter Eq(object? value) => BuildScalar(value);
    public QueryFilter Ne(object? value) => BuildOperator("$ne", value);
    public QueryFilter Gt(object? value) => BuildOperator("$gt", value);
    public QueryFilter Gte(object? value) => BuildOperator("$gte", value);
    public QueryFilter Lt(object? value) => BuildOperator("$lt", value);
    public QueryFilter Lte(object? value) => BuildOperator("$lte", value);
    public QueryFilter Contains(object? value) => BuildOperator("$contains", value);
    public QueryFilter StartsWith(object? value) => BuildOperator("$startsWith", value);
    public QueryFilter EndsWith(object? value) => BuildOperator("$endsWith", value);
    public QueryFilter Exists(bool value) => BuildOperator("$exists", value);
    public QueryFilter IsNull(bool value) => BuildOperator("$isNull", value);

    public QueryFilter In(params object?[] values) => BuildListOperator("$in", values);

    public QueryFilter Nin(params object?[] values) => BuildListOperator("$nin", values);

    private QueryFilter BuildScalar(object? value)
    {
        var root = new JsonObject
        {
            [_field] = ToNode(value)
        };

        return new QueryFilter(root);
    }

    private QueryFilter BuildOperator(string op, object? value)
    {
        var root = new JsonObject
        {
            [_field] = new JsonObject
            {
                [op] = ToNode(value)
            }
        };

        return new QueryFilter(root);
    }

    private QueryFilter BuildListOperator(string op, object?[] values)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        var array = new JsonArray(values.Select(ToNode).ToArray());
        var root = new JsonObject
        {
            [_field] = new JsonObject
            {
                [op] = array
            }
        };

        return new QueryFilter(root);
    }

    private static JsonNode? ToNode(object? value)
    {
        if (value is JsonElement element)
        {
            return JsonNode.Parse(element.GetRawText());
        }

        return JsonSerializer.SerializeToNode(value);
    }
}
