using System;
using System.Collections.Generic;
using System.Linq;
using DataQL.Ast.Model;

namespace DataQL.Validation;

public sealed class AstSemanticValidator : IAstValidator
{
    public ValidationResult Validate(QueryAst ast)
    {
        var errors = new List<ValidationError>();

        if (ast.Where is not null)
        {
            ValidateFilter(ast.Where, "where", errors);
        }

        ValidateOrdering(ast, errors);
        ValidateDistinct(ast, errors);

        if (ast.Group is not null)
        {
            ValidateGroup(ast.Group, "group", errors);
        }

        return new ValidationResult(errors);
    }

    private static void ValidateDistinct(QueryAst ast, ICollection<ValidationError> errors)
    {
        if (ast.Projection.Distinct.Count == 0)
        {
            return;
        }

        for (var i = 0; i < ast.Projection.Distinct.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(ast.Projection.Distinct[i].Value))
            {
                errors.Add(new ValidationError(
                    $"distinct[{i}]",
                    "Distinct.FieldRequired",
                    "distinct field cannot be empty."));
            }
        }

        if (ast.Group is not null)
        {
            errors.Add(new ValidationError(
                "distinct",
                "Distinct.GroupConflict",
                "distinct cannot be combined with group."));
        }

        if (ast.Projection.Exclude.Count > 0)
        {
            errors.Add(new ValidationError(
                "exclude",
                "Distinct.ExcludeConflict",
                "exclude cannot be combined with distinct."));
        }

        var distinctFields = new HashSet<string>(
            ast.Projection.Distinct.Select(f => f.Value),
            StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < ast.Projection.Select.Count; i++)
        {
            var field = ast.Projection.Select[i].Value;
            if (string.IsNullOrWhiteSpace(field))
            {
                continue;
            }

            if (!distinctFields.Contains(field))
            {
                errors.Add(new ValidationError(
                    $"select[{i}]",
                    "Distinct.SelectNotSubset",
                    "select fields must be a subset of distinct fields."));
            }
        }

        for (var i = 0; i < ast.Order.Count; i++)
        {
            var field = ast.Order[i].Field.Value;
            if (string.IsNullOrWhiteSpace(field))
            {
                continue;
            }

            if (!distinctFields.Contains(field))
            {
                errors.Add(new ValidationError(
                    $"order[{i}].field",
                    "Distinct.OrderNotSubset",
                    "order fields must be a subset of distinct fields."));
            }
        }
    }

    private static void ValidateFilter(FilterExpression filter, string path, ICollection<ValidationError> errors)
    {
        switch (filter)
        {
            case LogicalFilter logical:
                if (logical.Children.Count == 0)
                {
                    errors.Add(new ValidationError(path, "Filter.Logical.Empty", "Logical filter must contain at least one child."));
                }

                for (var i = 0; i < logical.Children.Count; i++)
                {
                    ValidateFilter(logical.Children[i], path + $".children[{i}]", errors);
                }
                break;

            case NotFilter not:
                ValidateFilter(not.Child, path + ".not", errors);
                break;

            case FieldFilter fieldFilter:
                ValidateFieldFilter(fieldFilter, path, errors);
                break;

            default:
                errors.Add(new ValidationError(path, "Filter.Unsupported", $"Unsupported filter type '{filter.GetType().Name}'."));
                break;
        }
    }

    private static void ValidateFieldFilter(FieldFilter fieldFilter, string path, ICollection<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(fieldFilter.Field.Value))
        {
            errors.Add(new ValidationError(path + ".field", "Filter.Field.Required", "Field path is required."));
        }

        if (fieldFilter.Operations.Count == 0)
        {
            errors.Add(new ValidationError(path + ".operations", "Filter.Operations.Empty", "Field filter must contain at least one operation."));
            return;
        }

        for (var i = 0; i < fieldFilter.Operations.Count; i++)
        {
            ValidateOperation(fieldFilter.Operations[i], path + $".operations[{i}]", errors);
        }
    }

    private static void ValidateOperation(FieldOperation operation, string path, ICollection<ValidationError> errors)
    {
        switch (operation)
        {
            case ScalarOperation scalar:
                ValidateScalarOperation(scalar, path, errors);
                break;

            case ListOperation list:
                ValidateListOperation(list, path, errors);
                break;

            case BooleanOperation boolean:
                ValidateBooleanOperation(boolean, path, errors);
                break;

            case IntegerOperation integer:
                ValidateIntegerOperation(integer, path, errors);
                break;

            case AnyOperation any:
                ValidateFilter(any.Predicate, path + ".any", errors);
                break;

            default:
                errors.Add(new ValidationError(path, "Filter.Operation.Unsupported", $"Unsupported operation type '{operation.GetType().Name}'."));
                break;
        }
    }

    private static void ValidateScalarOperation(ScalarOperation operation, string path, ICollection<ValidationError> errors)
    {
        if (operation.Value is not ScalarValue)
        {
            errors.Add(new ValidationError(path, "Filter.Operation.ScalarExpected", "Operation requires scalar value."));
            return;
        }

        if (operation.Operator is FieldOperator.In or FieldOperator.Nin or FieldOperator.ContainsAny or FieldOperator.ContainsAll
            or FieldOperator.Exists or FieldOperator.IsNull or FieldOperator.IsEmpty or FieldOperator.Size or FieldOperator.Any)
        {
            errors.Add(new ValidationError(path, "Filter.Operation.OperatorMismatch", $"Operator '{operation.Operator}' is not valid for scalar operation."));
        }
    }

    private static void ValidateListOperation(ListOperation operation, string path, ICollection<ValidationError> errors)
    {
        if (operation.Values.Count == 0)
        {
            errors.Add(new ValidationError(path, "Filter.Operation.ListEmpty", "List operation values cannot be empty."));
        }

        if (operation.Values.Any(v => v is not ScalarValue))
        {
            errors.Add(new ValidationError(path, "Filter.Operation.ListScalarOnly", "List operation values must be scalar."));
        }

        if (operation.Operator is not (FieldOperator.In or FieldOperator.Nin or FieldOperator.ContainsAny or FieldOperator.ContainsAll))
        {
            errors.Add(new ValidationError(path, "Filter.Operation.OperatorMismatch", $"Operator '{operation.Operator}' is not valid for list operation."));
        }
    }

    private static void ValidateBooleanOperation(BooleanOperation operation, string path, ICollection<ValidationError> errors)
    {
        if (operation.Operator is not (FieldOperator.Exists or FieldOperator.IsNull or FieldOperator.IsEmpty))
        {
            errors.Add(new ValidationError(path, "Filter.Operation.OperatorMismatch", $"Operator '{operation.Operator}' is not valid for boolean operation."));
        }
    }

    private static void ValidateIntegerOperation(IntegerOperation operation, string path, ICollection<ValidationError> errors)
    {
        if (operation.Operator != FieldOperator.Size)
        {
            errors.Add(new ValidationError(path, "Filter.Operation.OperatorMismatch", $"Operator '{operation.Operator}' is not valid for integer operation."));
        }

        if (operation.Value < 0)
        {
            errors.Add(new ValidationError(path, "Filter.Operation.IntegerOutOfRange", "Integer value must be greater than or equal to 0."));
        }
    }

    private static void ValidateOrdering(QueryAst ast, ICollection<ValidationError> errors)
    {
        for (var i = 0; i < ast.Order.Count; i++)
        {
            var order = ast.Order[i];
            if (string.IsNullOrWhiteSpace(order.Field.Value))
            {
                errors.Add(new ValidationError($"order[{i}].field", "Order.Field.Required", "Sort field cannot be empty."));
            }
        }

        if (ast.Pagination.Limit.HasValue && ast.Pagination.Limit.Value > 0 && ast.Pagination.ContinuationToken is not null && ast.Order.Count == 0)
        {
            errors.Add(new ValidationError("order", "Order.RequiredForContinuation", "Deterministic ordering is required when continuation token is supplied."));
        }
    }

    private static void ValidateGroup(GroupAst group, string path, ICollection<ValidationError> errors)
    {
        if (group.GroupBy.Count == 0)
        {
            errors.Add(new ValidationError(path + ".groupBy", "Group.GroupBy.Required", "groupBy must contain at least one field."));
        }

        for (var i = 0; i < group.GroupBy.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(group.GroupBy[i].Value))
            {
                errors.Add(new ValidationError(path + $".groupBy[{i}]", "Group.GroupBy.FieldRequired", "groupBy field cannot be empty."));
            }
        }

        if (group.Metrics.Count == 0)
        {
            errors.Add(new ValidationError(path + ".metrics", "Group.Metrics.Required", "metrics must contain at least one metric."));
            return;
        }

        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < group.Metrics.Count; i++)
        {
            ValidateMetric(group.Metrics[i], path + $".metrics[{i}]", aliases, errors);
        }
    }

    private static void ValidateMetric(GroupMetricAst metric, string path, ISet<string> aliases, ICollection<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(metric.Field.Value))
        {
            errors.Add(new ValidationError(path + ".field", "Group.Metric.FieldRequired", "metric field is required."));
        }

        if (string.IsNullOrWhiteSpace(metric.Alias))
        {
            errors.Add(new ValidationError(path + ".alias", "Group.Metric.AliasRequired", "metric alias is required."));
        }
        else if (!aliases.Add(metric.Alias))
        {
            errors.Add(new ValidationError(path + ".alias", "Group.Metric.AliasDuplicate", "metric alias must be unique."));
        }

        if (metric.Operation == GroupMetricOperation.Count)
        {
            return;
        }

        if (metric.Field.Value == "*")
        {
            errors.Add(new ValidationError(path + ".field", "Group.Metric.FieldWildcardInvalid", "Only count operation supports '*' field."));
        }
    }
}
