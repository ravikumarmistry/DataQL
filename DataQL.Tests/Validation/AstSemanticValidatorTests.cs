using DataQL.Ast.Model;
using DataQL.Validation;

namespace DataQL.Tests.Validation;

public class AstSemanticValidatorTests
{
    private readonly AstSemanticValidator _validator = new();

    [Fact]
    public void Validate_WithValidFilterAndGroup_ReturnsSuccess()
    {
        var ast = new QueryAst(
            new FieldFilter(new FieldPath("age"), [new ScalarOperation(FieldOperator.Gte, new ScalarValue(18))]),
            new ProjectionAst([], [], []),
            [new SortField(new FieldPath("createdAt"), SortDirection.Desc)],
            new PaginationAst(50, null, false, true),
            new GroupAst(
                [new FieldPath("city")],
                [new GroupMetricAst(new FieldPath("*"), GroupMetricOperation.Count, "employees")]));

        var result = _validator.Validate(ast);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithOperatorValueMismatch_ReturnsError()
    {
        var ast = new QueryAst(
            new FieldFilter(new FieldPath("skills"), [new ScalarOperation(FieldOperator.In, new ScalarValue("Azure"))]),
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            null);

        var result = _validator.Validate(ast);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "Filter.Operation.OperatorMismatch");
    }

    [Fact]
    public void Validate_WithNegativeSize_ReturnsError()
    {
        var ast = new QueryAst(
            new FieldFilter(new FieldPath("skills"), [new IntegerOperation(FieldOperator.Size, -1)]),
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            null);

        var result = _validator.Validate(ast);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "Filter.Operation.IntegerOutOfRange");
    }

    [Fact]
    public void Validate_WithEmptyLogicalNode_ReturnsError()
    {
        var ast = new QueryAst(
            new LogicalFilter(FilterLogicalOperator.And, []),
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            null);

        var result = _validator.Validate(ast);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "Filter.Logical.Empty");
    }

    [Fact]
    public void Validate_WithEmptyGroupBy_ReturnsError()
    {
        var group = new GroupAst(
            [],
            [new GroupMetricAst(new FieldPath("*"), GroupMetricOperation.Count, "employees")]);

        var ast = new QueryAst(null, new ProjectionAst([], [], []), [], new PaginationAst(null, null, false, false), group);

        var result = _validator.Validate(ast);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "Group.GroupBy.Required");
    }

    [Fact]
    public void Validate_WithDuplicateMetricAlias_ReturnsError()
    {
        var group = new GroupAst(
            [new FieldPath("department")],
            [
                new GroupMetricAst(new FieldPath("*"), GroupMetricOperation.Count, "employees"),
                new GroupMetricAst(new FieldPath("salary"), GroupMetricOperation.Sum, "employees")
            ]);

        var ast = new QueryAst(null, new ProjectionAst([], [], []), [], new PaginationAst(null, null, false, false), group);

        var result = _validator.Validate(ast);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "Group.Metric.AliasDuplicate");
    }

    [Fact]
    public void Validate_WithWildcardFieldOnNonCountMetric_ReturnsError()
    {
        var group = new GroupAst(
            [new FieldPath("department")],
            [new GroupMetricAst(new FieldPath("*"), GroupMetricOperation.Sum, "total")]);

        var ast = new QueryAst(null, new ProjectionAst([], [], []), [], new PaginationAst(null, null, false, false), group);

        var result = _validator.Validate(ast);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "Group.Metric.FieldWildcardInvalid");
    }

    [Fact]
    public void Validate_WithContinuationTokenAndNoOrder_ReturnsError()
    {
        var ast = new QueryAst(
            new FieldFilter(new FieldPath("age"), [new ScalarOperation(FieldOperator.Gte, new ScalarValue(18))]),
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(10, "token", false, true),
            null);

        var result = _validator.Validate(ast);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "Order.RequiredForContinuation");
    }

    [Fact]
    public void Validate_WithDistinctAndSelectSubset_ReturnsSuccess()
    {
        var ast = new QueryAst(
            null,
            new ProjectionAst(
                [new FieldPath("City")],
                [],
                [new FieldPath("City"), new FieldPath("Department")]),
            [new SortField(new FieldPath("City"), SortDirection.Asc)],
            new PaginationAst(null, null, false, false),
            null);

        var result = _validator.Validate(ast);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithDistinctAndSelectNotSubset_ReturnsError()
    {
        var ast = new QueryAst(
            null,
            new ProjectionAst(
                [new FieldPath("Name")],
                [],
                [new FieldPath("City")]),
            [new SortField(new FieldPath("City"), SortDirection.Asc)],
            new PaginationAst(null, null, false, false),
            null);

        var result = _validator.Validate(ast);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "Distinct.SelectNotSubset");
    }

    [Fact]
    public void Validate_WithDistinctAndOrderNotSubset_ReturnsError()
    {
        var ast = new QueryAst(
            null,
            new ProjectionAst([], [], [new FieldPath("City")]),
            [new SortField(new FieldPath("Name"), SortDirection.Asc)],
            new PaginationAst(null, null, false, false),
            null);

        var result = _validator.Validate(ast);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "Distinct.OrderNotSubset");
    }

    [Fact]
    public void Validate_WithDistinctAndGroup_ReturnsError()
    {
        var ast = new QueryAst(
            null,
            new ProjectionAst([], [], [new FieldPath("City")]),
            [new SortField(new FieldPath("City"), SortDirection.Asc)],
            new PaginationAst(null, null, false, false),
            new GroupAst(
                [new FieldPath("City")],
                [new GroupMetricAst(new FieldPath("*"), GroupMetricOperation.Count, "employees")]));

        var result = _validator.Validate(ast);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "Distinct.GroupConflict");
    }

    [Fact]
    public void Validate_WithDistinctAndExclude_ReturnsError()
    {
        var ast = new QueryAst(
            null,
            new ProjectionAst([], [new FieldPath("City")], [new FieldPath("City"), new FieldPath("Department")]),
            [new SortField(new FieldPath("City"), SortDirection.Asc)],
            new PaginationAst(null, null, false, false),
            null);

        var result = _validator.Validate(ast);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "Distinct.ExcludeConflict");
    }

    [Fact]
    public void Validate_WithEmptyDistinctField_ReturnsError()
    {
        var ast = new QueryAst(
            null,
            new ProjectionAst([], [], [new FieldPath(" ")]),
            [new SortField(new FieldPath("City"), SortDirection.Asc)],
            new PaginationAst(null, null, false, false),
            null);

        var result = _validator.Validate(ast);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "Distinct.FieldRequired");
    }
}
