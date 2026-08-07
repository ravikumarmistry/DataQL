using System;
using System.Collections.Generic;
using DataQL.Abstractions;
using DataQL.Ast.Model;

namespace DataQL.Validation;

public interface IProviderCapabilityValidator
{
    ValidationResult Validate(QueryAst ast, ProviderCapabilities capabilities);
}

public static class ProviderCapabilityValidatorExtensions
{
    public static void EnsureValid(
        this IProviderCapabilityValidator validator,
        QueryAst ast,
        ProviderCapabilities capabilities)
    {
        if (validator is null)
        {
            throw new ArgumentNullException(nameof(validator));
        }

        var result = validator.Validate(ast, capabilities);
        if (!result.IsValid)
        {
            throw new AstValidationException(result.Errors);
        }
    }
}

public sealed class ProviderCapabilityValidator : IProviderCapabilityValidator
{
    public static ProviderCapabilityValidator Instance { get; } = new();

    public ValidationResult Validate(QueryAst ast, ProviderCapabilities capabilities)
    {
        if (ast is null)
        {
            throw new ArgumentNullException(nameof(ast));
        }

        if (capabilities is null)
        {
            throw new ArgumentNullException(nameof(capabilities));
        }

        var errors = new List<ValidationError>();
        var provider = capabilities.Provider;

        if (ast.Where is not null)
        {
            ValidateFilter(ast.Where, "where", capabilities, errors);
        }

        if (ast.Projection.Select.Count > 0 && !capabilities.SupportsSelect)
        {
            errors.Add(new ValidationError(
                "select",
                "Capability.SelectNotSupported",
                $"Provider '{provider}' does not support 'select'.",
                provider));
        }

        if (ast.Projection.Exclude.Count > 0 && !capabilities.SupportsExclude)
        {
            errors.Add(new ValidationError(
                "exclude",
                "Capability.ExcludeNotSupported",
                $"Provider '{provider}' does not support 'exclude'.",
                provider));
        }

        foreach (var field in ast.Projection.Select)
        {
            ValidateFieldPath(field.Value, "select", capabilities, errors);
        }

        foreach (var field in ast.Projection.Exclude)
        {
            ValidateFieldPath(field.Value, "exclude", capabilities, errors);
        }

        if (ast.Projection.Distinct.Count > 0)
        {
            if (!capabilities.SupportsDistinct)
            {
                errors.Add(new ValidationError(
                    "distinct",
                    "Capability.DistinctNotSupported",
                    $"Provider '{provider}' does not support 'distinct'.",
                    provider));
            }

            for (var i = 0; i < ast.Projection.Distinct.Count; i++)
            {
                ValidateFieldPath(
                    ast.Projection.Distinct[i].Value,
                    $"distinct[{i}]",
                    capabilities,
                    errors);
            }
        }

        for (var i = 0; i < ast.Order.Count; i++)
        {
            ValidateFieldPath(ast.Order[i].Field.Value, $"order[{i}].field", capabilities, errors);
        }

        if (ast.Group is not null)
        {
            ValidateGroup(ast.Group, "group", capabilities, errors);
        }

        return errors.Count == 0 ? ValidationResult.Success() : new ValidationResult(errors);
    }

    private static void ValidateGroup(
        GroupAst group,
        string path,
        ProviderCapabilities capabilities,
        ICollection<ValidationError> errors)
    {
        if (!capabilities.SupportsGrouping)
        {
            errors.Add(new ValidationError(
                path,
                "Capability.GroupingNotSupported",
                $"Provider '{capabilities.Provider}' does not support grouping.",
                capabilities.Provider));
            return;
        }

        for (var i = 0; i < group.GroupBy.Count; i++)
        {
            ValidateFieldPath(group.GroupBy[i].Value, $"{path}.groupBy[{i}]", capabilities, errors);
        }

        for (var i = 0; i < group.Metrics.Count; i++)
        {
            var metric = group.Metrics[i];
            var metricPath = $"{path}.metrics[{i}]";
            var operationName = ToGroupOperationName(metric.Operation);
            if (!capabilities.SupportedGroupOperations.Contains(operationName))
            {
                errors.Add(new ValidationError(
                    metricPath + ".operation",
                    "Capability.GroupOperationNotSupported",
                    $"Provider '{capabilities.Provider}' does not support group operation '{operationName}'.",
                    capabilities.Provider,
                    new Dictionary<string, object?>
                    {
                        ["operation"] = operationName,
                        ["supportedGroupOperations"] = capabilities.SupportedGroupOperations
                    }));
            }

            if (!string.Equals(metric.Field.Value, "*", StringComparison.Ordinal))
            {
                ValidateFieldPath(metric.Field.Value, metricPath + ".field", capabilities, errors);
            }
        }

        if (group.Having is not null)
        {
            if (!capabilities.SupportsHaving)
            {
                errors.Add(new ValidationError(
                    path + ".having",
                    "Capability.HavingNotSupported",
                    $"Provider '{capabilities.Provider}' does not support 'having'.",
                    capabilities.Provider));
            }
            else
            {
                // Having fields are aliases/group keys — do not apply nested-path/operator field checks the same way.
                ValidateHavingFilter(group.Having, path + ".having", capabilities, errors);
            }
        }
    }

    private static void ValidateHavingFilter(
        FilterExpression filter,
        string path,
        ProviderCapabilities capabilities,
        ICollection<ValidationError> errors)
    {
        switch (filter)
        {
            case LogicalFilter logical:
                for (var i = 0; i < logical.Children.Count; i++)
                {
                    ValidateHavingFilter(logical.Children[i], $"{path}[{i}]", capabilities, errors);
                }
                break;
            case NotFilter not:
                ValidateHavingFilter(not.Child, path + ".$not", capabilities, errors);
                break;
            case FieldFilter fieldFilter:
                foreach (var operation in fieldFilter.Operations)
                {
                    ValidateOperator(
                        operation.Operator,
                        $"{path}.{fieldFilter.Field.Value}",
                        capabilities,
                        errors);
                }
                break;
        }
    }

    private static void ValidateFilter(
        FilterExpression filter,
        string path,
        ProviderCapabilities capabilities,
        ICollection<ValidationError> errors)
    {
        switch (filter)
        {
            case LogicalFilter logical:
                for (var i = 0; i < logical.Children.Count; i++)
                {
                    ValidateFilter(logical.Children[i], $"{path}[{i}]", capabilities, errors);
                }
                break;
            case NotFilter not:
                ValidateFilter(not.Child, path + ".$not", capabilities, errors);
                break;
            case FieldFilter fieldFilter:
                ValidateFieldPath(fieldFilter.Field.Value, path + "." + fieldFilter.Field.Value, capabilities, errors);
                for (var i = 0; i < fieldFilter.Operations.Count; i++)
                {
                    var operation = fieldFilter.Operations[i];
                    var opPath = $"{path}.{fieldFilter.Field.Value}";
                    ValidateOperator(operation.Operator, opPath, capabilities, errors);
                    if (operation is AnyOperation any)
                    {
                        ValidateFilter(any.Predicate, opPath + ".$any", capabilities, errors);
                    }
                }
                break;
        }
    }

    private static void ValidateOperator(
        FieldOperator op,
        string path,
        ProviderCapabilities capabilities,
        ICollection<ValidationError> errors)
    {
        var opName = ToOperatorName(op);
        if (capabilities.SupportedOperators.Contains(opName))
        {
            return;
        }

        errors.Add(new ValidationError(
            path + "." + opName,
            "Capability.OperatorNotSupported",
            $"Provider '{capabilities.Provider}' does not support operator '{opName}'.",
            capabilities.Provider,
            new Dictionary<string, object?>
            {
                ["operator"] = opName,
                ["supportedOperators"] = capabilities.SupportedOperators
            }));
    }

    private static void ValidateFieldPath(
        string fieldPath,
        string path,
        ProviderCapabilities capabilities,
        ICollection<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(fieldPath))
        {
            return;
        }

        if (fieldPath.Contains('.', StringComparison.Ordinal) && !capabilities.SupportsNestedFields)
        {
            errors.Add(new ValidationError(
                path,
                "Capability.NestedFieldsNotSupported",
                $"Provider '{capabilities.Provider}' does not support nested field paths.",
                capabilities.Provider,
                new Dictionary<string, object?>
                {
                    ["field"] = fieldPath
                }));
        }
    }

    private static string ToOperatorName(FieldOperator op) => op switch
    {
        FieldOperator.Eq => "$eq",
        FieldOperator.Ne => "$ne",
        FieldOperator.Gt => "$gt",
        FieldOperator.Gte => "$gte",
        FieldOperator.Lt => "$lt",
        FieldOperator.Lte => "$lte",
        FieldOperator.In => "$in",
        FieldOperator.Nin => "$nin",
        FieldOperator.Contains => "$contains",
        FieldOperator.StartsWith => "$startsWith",
        FieldOperator.EndsWith => "$endsWith",
        FieldOperator.Regex => "$regex",
        FieldOperator.Exists => "$exists",
        FieldOperator.IsNull => "$isNull",
        FieldOperator.ContainsAny => "$containsAny",
        FieldOperator.ContainsAll => "$containsAll",
        FieldOperator.Size => "$size",
        FieldOperator.IsEmpty => "$isEmpty",
        FieldOperator.Any => "$any",
        _ => op.ToString()
    };

    private static string ToGroupOperationName(GroupMetricOperation operation) => operation switch
    {
        GroupMetricOperation.Count => "count",
        GroupMetricOperation.Sum => "sum",
        GroupMetricOperation.Avg => "avg",
        GroupMetricOperation.Min => "min",
        GroupMetricOperation.Max => "max",
        GroupMetricOperation.First => "first",
        GroupMetricOperation.Last => "last",
        _ => operation.ToString().ToLowerInvariant()
    };
}
