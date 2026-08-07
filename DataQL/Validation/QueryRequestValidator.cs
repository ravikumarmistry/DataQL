using System.Collections.Generic;
using System;
using DataQL.Contracts;

namespace DataQL.Validation;

public sealed class QueryRequestValidator : IQueryValidator
{
    private readonly int _maxLimit;

    public QueryRequestValidator(int maxLimit = 500)
    {
        _maxLimit = maxLimit;
    }

    public ValidationResult Validate(QueryRequest request)
    {
        var errors = new List<ValidationError>();

        if (request.Order.Count == 0)
        {
            errors.Add(new ValidationError("order", "Order.Required", "order must contain at least one sort field."));
        }

        if (request.Limit is < 1)
        {
            errors.Add(new ValidationError("limit", "Limit.OutOfRange", "limit must be greater than 0 when provided."));
        }

        if (request.Limit is > 0 && request.Limit > _maxLimit)
        {
            errors.Add(new ValidationError("limit", "Limit.MaxExceeded", $"limit must be less than or equal to {_maxLimit}."));
        }

        if (!string.IsNullOrWhiteSpace(request.ContinuationToken) && request.Limit is not > 0)
        {
            errors.Add(new ValidationError("continuationToken", "ContinuationToken.RequiresLimit", "continuationToken requires a positive limit."));
        }

        foreach (var order in request.Order)
        {
            if (string.IsNullOrWhiteSpace(order.Field))
            {
                errors.Add(new ValidationError("order.field", "Order.FieldRequired", "order field is required."));
            }

            var direction = order.Direction.ToLowerInvariant();
            if (direction is not ("asc" or "desc"))
            {
                errors.Add(new ValidationError("order.direction", "Order.DirectionInvalid", "order direction must be 'asc' or 'desc'."));
            }
        }

        if (request.Group is not null)
        {
            ValidateGroup(request.Group, errors);
        }

        return new ValidationResult(errors);
    }

    private static void ValidateGroup(GroupRequest group, ICollection<ValidationError> errors)
    {
        if (group.GroupBy.Count == 0)
        {
            errors.Add(new ValidationError("group.groupBy", "Group.GroupBy.Required", "groupBy must contain at least one field."));
        }

        for (var i = 0; i < group.GroupBy.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(group.GroupBy[i]))
            {
                errors.Add(new ValidationError($"group.groupBy[{i}]", "Group.GroupBy.FieldRequired", "groupBy field cannot be empty."));
            }
        }

        if (group.Metrics.Count == 0)
        {
            errors.Add(new ValidationError("group.metrics", "Group.Metrics.Required", "metrics must contain at least one metric."));
            return;
        }

        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < group.Metrics.Count; i++)
        {
            ValidateMetric(group.Metrics[i], i, aliases, errors);
        }
    }

    private static void ValidateMetric(
        GroupMetricRequest metric,
        int index,
        ISet<string> aliases,
        ICollection<ValidationError> errors)
    {
        var path = $"group.metrics[{index}]";

        var field = metric.Field ?? string.Empty;
        if (string.IsNullOrWhiteSpace(field))
        {
            errors.Add(new ValidationError(path + ".field", "Group.Metric.FieldRequired", "metric field is required."));
        }

        var alias = metric.Alias ?? string.Empty;
        if (string.IsNullOrWhiteSpace(alias))
        {
            errors.Add(new ValidationError(path + ".alias", "Group.Metric.AliasRequired", "metric alias is required."));
        }
        else if (!aliases.Add(alias))
        {
            errors.Add(new ValidationError(path + ".alias", "Group.Metric.AliasDuplicate", "metric alias must be unique."));
        }

        var operation = ParseOperation(metric.Operation);
        if (operation is null)
        {
            errors.Add(new ValidationError(path + ".operation", "Group.Metric.OperationInvalid", "metric operation is invalid."));
            return;
        }

        if (operation != "count" && field == "*")
        {
            errors.Add(new ValidationError(path + ".field", "Group.Metric.FieldWildcardInvalid", "Only count operation supports '*' field."));
        }
    }

    private static string? ParseOperation(string? operation)
    {
        return (operation ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "count" => "count",
            "sum" => "sum",
            "avg" => "avg",
            "min" => "min",
            "max" => "max",
            "first" => "first",
            "last" => "last",
            _ => null
        };
    }
}
