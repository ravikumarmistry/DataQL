using DataQL.Ast.Model;
using DataQL.Abstractions;
using DataQL.Cosmos.Translation;

namespace DataQL.Cosmos.Tests.Translation;

public class CosmosQueryTranslatorGroupTests
{
    [Fact]
    public void ProviderTranslate_WithGroupAndWhere_BuildsGroupBySql()
    {
        var translator = new DataQL.Cosmos.CosmosQueryTranslator();
        var ast = new QueryAst(
            new FieldFilter(new FieldPath("active"), [new ScalarOperation(FieldOperator.Eq, new ScalarValue(true))]),
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            new GroupAst(
                [new FieldPath("department")],
                [
                    new GroupMetricAst(new FieldPath("*"), GroupMetricOperation.Count, "employees")
                ]));

        var source = new QuerySource(ProviderName.Cosmos, "EmployeesContainer");
        var result = Assert.IsType<CosmosSqlTranslationResult>(translator.Translate(ast, source));

        Assert.Contains("GROUP BY c.department", result.Sql);
    }

    [Fact]
    public void Translate_WithGroupAndWhere_BuildsGroupBySql()
    {
        var translator = new CosmosSqlTranslator();
        var ast = new QueryAst(
            new FieldFilter(new FieldPath("active"), [new ScalarOperation(FieldOperator.Eq, new ScalarValue(true))]),
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            new GroupAst(
                [new FieldPath("department")],
                [
                    new GroupMetricAst(new FieldPath("*"), GroupMetricOperation.Count, "employees"),
                    new GroupMetricAst(new FieldPath("salary"), GroupMetricOperation.Avg, "averageSalary")
                ]));

        var result = translator.Translate(ast);

        Assert.Contains("GROUP BY c.department", result.Sql);
        Assert.Contains("COUNT(1)", result.Sql);
        Assert.Contains("AVG(c.salary)", result.Sql);
        Assert.Contains("WHERE c.active = @p0", result.Sql);
        Assert.Equal(true, result.Parameters["@p0"]);
    }

    [Fact]
    public void Translate_WithGroupOrderAndLimit_WrapsGroupedQuery()
    {
        var translator = new CosmosSqlTranslator();
        var ast = new QueryAst(
            null,
            new ProjectionAst([], [], []),
            [new SortField(new FieldPath("employees"), SortDirection.Desc)],
            new PaginationAst(10, null, false, true),
            new GroupAst(
                [new FieldPath("department")],
                [new GroupMetricAst(new FieldPath("*"), GroupMetricOperation.Count, "employees")]));

        var result = translator.Translate(ast);

        Assert.StartsWith("SELECT * FROM (", result.Sql);
        Assert.Contains("ORDER BY g.employees DESC", result.Sql);
        Assert.Contains("OFFSET 0 LIMIT 10", result.Sql);
    }

    [Fact]
    public void Translate_WithFirstMetric_ThrowsNotSupportedException()
    {
        var translator = new CosmosSqlTranslator();
        var ast = new QueryAst(
            null,
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            new GroupAst(
                [new FieldPath("department")],
                [new GroupMetricAst(new FieldPath("salary"), GroupMetricOperation.First, "firstSalary")]));

        Assert.Throws<NotSupportedException>(() => translator.Translate(ast));
    }

    [Fact]
    public void Translate_WithHaving_ThrowsNotSupportedException()
    {
        var translator = new CosmosSqlTranslator();
        var having = new FieldFilter(
            new FieldPath("employees"),
            [new ScalarOperation(FieldOperator.Gte, new ScalarValue(2))]);
        var ast = new QueryAst(
            null,
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            new GroupAst(
                [new FieldPath("department")],
                [new GroupMetricAst(new FieldPath("*"), GroupMetricOperation.Count, "employees")],
                having));

        var ex = Assert.Throws<NotSupportedException>(() => translator.Translate(ast));
        Assert.Contains("having", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
