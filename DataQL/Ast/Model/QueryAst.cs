using System.Collections.Generic;

namespace DataQL.Ast.Model;

public sealed record QueryAst(
    FilterExpression? Where,
    ProjectionAst Projection,
    IReadOnlyList<SortField> Order,
    PaginationAst Pagination,
    GroupAst? Group);

public sealed record ProjectionAst(
    IReadOnlyList<FieldPath> Select,
    IReadOnlyList<FieldPath> Exclude,
    IReadOnlyList<FieldPath> Distinct);

public sealed record PaginationAst(
    int? Limit,
    string? ContinuationToken,
    bool IncludeCount,
    bool RequiresDeterministicOrder);

public readonly record struct FieldPath(string Value);

public sealed record SortField(FieldPath Field, SortDirection Direction);

public enum SortDirection
{
    Asc,
    Desc
}

public abstract record FilterExpression;

public sealed record LogicalFilter(FilterLogicalOperator Operator, IReadOnlyList<FilterExpression> Children) : FilterExpression;

public sealed record NotFilter(FilterExpression Child) : FilterExpression;

public sealed record FieldFilter(FieldPath Field, IReadOnlyList<FieldOperation> Operations) : FilterExpression;

public enum FilterLogicalOperator
{
    And,
    Or
}

public enum FieldOperator
{
    Eq,
    Ne,
    Gt,
    Gte,
    Lt,
    Lte,
    In,
    Nin,
    Contains,
    StartsWith,
    EndsWith,
    Regex,
    Exists,
    IsNull,
    ContainsAny,
    ContainsAll,
    Size,
    IsEmpty,
    Any
}

public abstract record FieldOperation(FieldOperator Operator);

public sealed record ScalarOperation(FieldOperator Operator, AstValue Value) : FieldOperation(Operator);

public sealed record ListOperation(FieldOperator Operator, IReadOnlyList<AstValue> Values) : FieldOperation(Operator);

public sealed record BooleanOperation(FieldOperator Operator, bool Value) : FieldOperation(Operator);

public sealed record IntegerOperation(FieldOperator Operator, int Value) : FieldOperation(Operator);

public sealed record AnyOperation(FilterExpression Predicate) : FieldOperation(FieldOperator.Any);

public abstract record AstValue;

public sealed record ScalarValue(object? Value) : AstValue;

public sealed record ArrayValue(IReadOnlyList<AstValue> Values) : AstValue;

public sealed record ObjectValue(IReadOnlyDictionary<string, AstValue> Properties) : AstValue;

public sealed record GroupAst(
    IReadOnlyList<FieldPath> GroupBy,
    IReadOnlyList<GroupMetricAst> Metrics,
    FilterExpression? Having = null);

public sealed record GroupMetricAst(
    FieldPath Field,
    GroupMetricOperation Operation,
    string Alias);

public enum GroupMetricOperation
{
    Count,
    Avg,
    Sum,
    Min,
    Max,
    First,
    Last
}
