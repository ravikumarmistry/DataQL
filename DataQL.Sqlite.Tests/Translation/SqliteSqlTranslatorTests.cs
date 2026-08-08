using DataQL.Ast.Model;
using DataQL.Sqlite.Translation;

namespace DataQL.Sqlite.Tests.Translation;

public class SqliteSqlTranslatorTests
{
    [Fact]
    public void Translate_WithComparisonFilter_BuildsParameterizedSql()
    {
        var translator = new SqliteSqlTranslator();
        var ast = new QueryAst(
            new FieldFilter(new FieldPath("Age"), [new ScalarOperation(FieldOperator.Gte, new ScalarValue(18))]),
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            null);

        var result = translator.Translate(ast, "Employees");

        Assert.Contains("FROM \"Employees\" AS \"t\"", result.Sql);
        Assert.Contains("\"t\".\"Age\" >= @p0", result.Sql);
        Assert.Equal(18, result.Parameters["@p0"]);
    }

    [Fact]
    public void Translate_WithSelectAndExclude_BuildsProjectedSql()
    {
        var translator = new SqliteSqlTranslator();
        var ast = new QueryAst(
            null,
            new ProjectionAst([new FieldPath("Name"), new FieldPath("City")], [new FieldPath("City")], []),
            [],
            new PaginationAst(null, null, false, false),
            null);

        var result = translator.Translate(ast, "Employees");

        Assert.StartsWith("SELECT \"t\".\"Name\" AS \"Name\" FROM \"Employees\" AS \"t\"", result.Sql);
    }

    [Fact]
    public void Translate_WithExcludeWithoutSelect_ThrowsNotSupportedException()
    {
        var translator = new SqliteSqlTranslator();
        var ast = new QueryAst(
            null,
            new ProjectionAst([], [new FieldPath("City")], []),
            [],
            new PaginationAst(null, null, false, false),
            null);

        Assert.Throws<NotSupportedException>(() => translator.Translate(ast, "Employees"));
    }

    [Fact]
    public void Translate_WithOrderAndLimit_UsesLimitAndOrderBy()
    {
        var translator = new SqliteSqlTranslator();
        var ast = new QueryAst(
            null,
            new ProjectionAst([], [], []),
            [new SortField(new FieldPath("CreatedAt"), SortDirection.Desc)],
            new PaginationAst(10, null, false, true),
            null);

        var result = translator.Translate(ast, "Employees");

        Assert.Contains("ORDER BY \"t\".\"CreatedAt\" DESC", result.Sql);
        Assert.EndsWith("LIMIT 10", result.Sql);
    }

    [Fact]
    public void Translate_WithGroupedMetrics_BuildsGroupSql()
    {
        var translator = new SqliteSqlTranslator();
        var ast = new QueryAst(
            new FieldFilter(new FieldPath("IsActive"), [new ScalarOperation(FieldOperator.Eq, new ScalarValue(true))]),
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            new GroupAst(
                [new FieldPath("Department")],
                [
                    new GroupMetricAst(new FieldPath("*"), GroupMetricOperation.Count, "employees"),
                    new GroupMetricAst(new FieldPath("Salary"), GroupMetricOperation.Avg, "avgSalary")
                ]));

        var result = translator.Translate(ast, "Employees");

        Assert.Contains("GROUP BY \"t\".\"Department\"", result.Sql);
        Assert.Contains("COUNT(1) AS \"employees\"", result.Sql);
        Assert.Contains("AVG(\"t\".\"Salary\") AS \"avgSalary\"", result.Sql);
        Assert.Contains("WHERE \"t\".\"IsActive\" = @p0", result.Sql);
    }

    [Fact]
    public void Translate_WithHavingOnMetricAlias_BuildsHavingSql()
    {
        var translator = new SqliteSqlTranslator();
        var having = new FieldFilter(
            new FieldPath("employees"),
            [new ScalarOperation(FieldOperator.Gte, new ScalarValue(2))]);
        var ast = new QueryAst(
            null,
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            new GroupAst(
                [new FieldPath("Department")],
                [new GroupMetricAst(new FieldPath("*"), GroupMetricOperation.Count, "employees")],
                having));

        var result = translator.Translate(ast, "Employees");

        Assert.Contains("GROUP BY \"t\".\"Department\"", result.Sql);
        Assert.Contains("HAVING COUNT(1) >= @p0", result.Sql);
        Assert.Equal(2, Convert.ToInt32(result.Parameters["@p0"]));
    }

    [Fact]
    public void Translate_WithUnknownHavingField_ThrowsNotSupportedException()
    {
        var translator = new SqliteSqlTranslator();
        var having = new FieldFilter(
            new FieldPath("missing"),
            [new ScalarOperation(FieldOperator.Gt, new ScalarValue(1))]);
        var ast = new QueryAst(
            null,
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            new GroupAst(
                [new FieldPath("Department")],
                [new GroupMetricAst(new FieldPath("*"), GroupMetricOperation.Count, "employees")],
                having));

        var ex = Assert.Throws<NotSupportedException>(() => translator.Translate(ast, "Employees"));
        Assert.Contains("missing", ex.Message);
    }

    [Fact]
    public void Translate_WithFirstMetric_ThrowsNotSupportedException()
    {
        var translator = new SqliteSqlTranslator();
        var ast = new QueryAst(
            null,
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            new GroupAst(
                [new FieldPath("Department")],
                [new GroupMetricAst(new FieldPath("Salary"), GroupMetricOperation.First, "firstSalary")])) ;

        Assert.Throws<NotSupportedException>(() => translator.Translate(ast, "Employees"));
    }

    [Fact]
    public void Translate_WithLastMetric_ThrowsNotSupportedException()
    {
        var translator = new SqliteSqlTranslator();
        var ast = new QueryAst(
            null,
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            new GroupAst(
                [new FieldPath("Department")],
                [new GroupMetricAst(new FieldPath("Age"), GroupMetricOperation.Last, "lastAge")]));

        var ex = Assert.Throws<NotSupportedException>(() => translator.Translate(ast, "Employees"));
        Assert.Contains("last", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Translate_WithDistinct_BuildsSelectDistinctOverAllDistinctFields()
    {
        var translator = new SqliteSqlTranslator();
        var ast = new QueryAst(
            null,
            new ProjectionAst(
                [new FieldPath("City")],
                [],
                [new FieldPath("City"), new FieldPath("Department")]),
            [new SortField(new FieldPath("City"), SortDirection.Asc)],
            new PaginationAst(10, null, false, true),
            null);

        var result = translator.Translate(ast, "Employees");

        Assert.StartsWith(
            "SELECT DISTINCT \"t\".\"City\" AS \"City\", \"t\".\"Department\" AS \"Department\" FROM \"Employees\" AS \"t\"",
            result.Sql);
        Assert.Contains("ORDER BY \"t\".\"City\" ASC", result.Sql);
        Assert.EndsWith("LIMIT 10", result.Sql);
    }

    [Fact]
    public void Translate_WithEmptyTableName_ThrowsArgumentException()
    {
        var translator = new SqliteSqlTranslator();
        var ast = new QueryAst(
            null,
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            null);

        Assert.Throws<ArgumentException>(() => translator.Translate(ast, "  "));
    }

    [Fact]
    public void Translate_WithNestedField_ThrowsNotSupportedException()
    {
        var translator = new SqliteSqlTranslator();
        var ast = new QueryAst(
            new FieldFilter(new FieldPath("Address.City"), [new ScalarOperation(FieldOperator.Eq, new ScalarValue("Delhi"))]),
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            null);

        var ex = Assert.Throws<NotSupportedException>(() => translator.Translate(ast, "Employees"));
        Assert.Contains("single-segment", ex.Message);
    }

    [Fact]
    public void Translate_WithAnyOperation_ThrowsNotSupportedException()
    {
        var translator = new SqliteSqlTranslator();
        var predicate = new FieldFilter(
            new FieldPath("Tag"),
            [new ScalarOperation(FieldOperator.Eq, new ScalarValue("x"))]);
        var ast = new QueryAst(
            new FieldFilter(new FieldPath("Tags"), [new AnyOperation(predicate)]),
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            null);

        var ex = Assert.Throws<NotSupportedException>(() => translator.Translate(ast, "Employees"));
        Assert.Contains("$any", ex.Message);
    }

    [Fact]
    public void Translate_WithIntegerArrayOperation_ThrowsNotSupportedException()
    {
        var translator = new SqliteSqlTranslator();
        var ast = new QueryAst(
            new FieldFilter(new FieldPath("Tags"), [new IntegerOperation(FieldOperator.Size, 2)]),
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            null);

        var ex = Assert.Throws<NotSupportedException>(() => translator.Translate(ast, "Employees"));
        Assert.Contains("integer array", ex.Message);
    }

    [Fact]
    public void Translate_WithGroupedLimit_WrapsOuterSelect()
    {
        var translator = new SqliteSqlTranslator();
        var ast = new QueryAst(
            null,
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(5, null, false, true),
            new GroupAst(
                [new FieldPath("Department")],
                [new GroupMetricAst(new FieldPath("*"), GroupMetricOperation.Count, "employees")]));

        var result = translator.Translate(ast, "Employees");

        Assert.StartsWith("SELECT * FROM (", result.Sql);
        Assert.Contains("GROUP BY \"t\".\"Department\"", result.Sql);
        Assert.EndsWith("LIMIT 5", result.Sql);
    }
}
