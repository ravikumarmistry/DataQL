using DataQL.Ast.Model;
using DataQL.SqlServer.Translation;

namespace DataQL.SqlServer.Tests.Translation;

public class SqlServerSqlTranslatorTests
{
    [Fact]
    public void Translate_WithComparisonFilter_BuildsParameterizedSql()
    {
        var translator = new SqlServerSqlTranslator();
        var ast = new QueryAst(
            new FieldFilter(new FieldPath("Age"), [new ScalarOperation(FieldOperator.Gte, new ScalarValue(18))]),
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            null);

        var result = translator.Translate(ast, "Employees");

        Assert.Contains("FROM [Employees] AS [t]", result.Sql);
        Assert.Contains("[t].[Age] >= @p0", result.Sql);
        Assert.Equal(18, result.Parameters["@p0"]);
    }

    [Fact]
    public void Translate_WithSelectAndExclude_BuildsProjectedSql()
    {
        var translator = new SqlServerSqlTranslator();
        var ast = new QueryAst(
            null,
            new ProjectionAst([new FieldPath("Name"), new FieldPath("City")], [new FieldPath("City")], []),
            [],
            new PaginationAst(null, null, false, false),
            null);

        var result = translator.Translate(ast, "Employees");

        Assert.StartsWith("SELECT [t].[Name] AS [Name] FROM [Employees] AS [t]", result.Sql);
    }

    [Fact]
    public void Translate_WithExcludeWithoutSelect_ThrowsNotSupportedException()
    {
        var translator = new SqlServerSqlTranslator();
        var ast = new QueryAst(
            null,
            new ProjectionAst([], [new FieldPath("City")], []),
            [],
            new PaginationAst(null, null, false, false),
            null);

        Assert.Throws<NotSupportedException>(() => translator.Translate(ast, "Employees"));
    }

    [Fact]
    public void Translate_WithOrderAndLimit_UsesTopAndOrderBy()
    {
        var translator = new SqlServerSqlTranslator();
        var ast = new QueryAst(
            null,
            new ProjectionAst([], [], []),
            [new SortField(new FieldPath("CreatedAt"), SortDirection.Desc)],
            new PaginationAst(10, null, false, true),
            null);

        var result = translator.Translate(ast, "Employees");

        Assert.StartsWith("SELECT TOP (10) [t].*", result.Sql);
        Assert.Contains("ORDER BY [t].[CreatedAt] DESC", result.Sql);
    }

    [Fact]
    public void Translate_WithQualifiedTableName_QuotesEachPart()
    {
        var translator = new SqlServerSqlTranslator();
        var ast = new QueryAst(
            null,
            new ProjectionAst([], [], []),
            [new SortField(new FieldPath("Id"), SortDirection.Asc)],
            new PaginationAst(2, null, false, true),
            null);

        var result = translator.Translate(ast, "dbo.PsAssignmentResourceRequests");

        Assert.Contains("FROM [dbo].[PsAssignmentResourceRequests] AS [t]", result.Sql);
        Assert.Contains("ORDER BY [t].[Id] ASC", result.Sql);
        Assert.StartsWith("SELECT TOP (2) [t].*", result.Sql);
    }

    [Fact]
    public void Translate_WithGroupedMetrics_BuildsGroupSql()
    {
        var translator = new SqlServerSqlTranslator();
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

        Assert.Contains("GROUP BY [t].[Department]", result.Sql);
        Assert.Contains("COUNT(1) AS [employees]", result.Sql);
        Assert.Contains("AVG(CAST([t].[Salary] AS float)) AS [avgSalary]", result.Sql);
        Assert.Contains("WHERE [t].[IsActive] = @p0", result.Sql);
    }

    [Fact]
    public void Translate_WithHavingOnMetricAlias_BuildsHavingSql()
    {
        var translator = new SqlServerSqlTranslator();
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

        Assert.Contains("GROUP BY [t].[Department]", result.Sql);
        Assert.Contains("HAVING COUNT(1) >= @p0", result.Sql);
        Assert.Equal(2, Convert.ToInt32(result.Parameters["@p0"]));
    }

    [Fact]
    public void Translate_WithHavingAndOr_BuildsLogicalHavingSql()
    {
        var translator = new SqlServerSqlTranslator();
        var having = new LogicalFilter(
            FilterLogicalOperator.And,
            [
                new FieldFilter(
                    new FieldPath("employees"),
                    [new ScalarOperation(FieldOperator.Gte, new ScalarValue(2))]),
                new LogicalFilter(
                    FilterLogicalOperator.Or,
                    [
                        new FieldFilter(
                            new FieldPath("Department"),
                            [new ScalarOperation(FieldOperator.Eq, new ScalarValue("Engineering"))]),
                        new FieldFilter(
                            new FieldPath("employees"),
                            [new ScalarOperation(FieldOperator.Gte, new ScalarValue(5))])
                    ])
            ]);
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

        Assert.Contains(
            "HAVING (COUNT(1) >= @p0) AND (([t].[Department] = @p1) OR (COUNT(1) >= @p2))",
            result.Sql);
        Assert.Equal(2, Convert.ToInt32(result.Parameters["@p0"]));
        Assert.Equal("Engineering", result.Parameters["@p1"]);
        Assert.Equal(5, Convert.ToInt32(result.Parameters["@p2"]));
    }

    [Fact]
    public void Translate_WithHavingNot_BuildsNegatedHavingSql()
    {
        var translator = new SqlServerSqlTranslator();
        var having = new NotFilter(
            new FieldFilter(
                new FieldPath("employees"),
                [new ScalarOperation(FieldOperator.Lt, new ScalarValue(2))]));
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

        Assert.Contains("HAVING NOT (COUNT(1) < @p0)", result.Sql);
        Assert.Equal(2, Convert.ToInt32(result.Parameters["@p0"]));
    }

    [Fact]
    public void Translate_WithLastMetric_ThrowsNotSupportedException()
    {
        var translator = new SqlServerSqlTranslator();
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
    public void Translate_WithUnknownHavingField_ThrowsNotSupportedException()
    {
        var translator = new SqlServerSqlTranslator();
        var having = new FieldFilter(
            new FieldPath("missingAlias"),
            [new ScalarOperation(FieldOperator.Gte, new ScalarValue(1))]);
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
        Assert.Contains("missingAlias", ex.Message);
    }

    [Fact]
    public void Translate_WithEmptyContainsAny_BuildsFalsePredicate()
    {
        var translator = new SqlServerSqlTranslator();
        var ast = new QueryAst(
            new FieldFilter(
                new FieldPath("Tags"),
                [new ListOperation(FieldOperator.ContainsAny, [])]),
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            null);

        var result = translator.Translate(ast, "Employees");
        Assert.Contains("1 = 0", result.Sql);
    }

    [Fact]
    public void Translate_WithFirstMetric_ThrowsNotSupportedException()
    {
        var translator = new SqlServerSqlTranslator();
        var ast = new QueryAst(
            null,
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            new GroupAst(
                [new FieldPath("Department")],
                [new GroupMetricAst(new FieldPath("Salary"), GroupMetricOperation.First, "firstSalary")]));

        Assert.Throws<NotSupportedException>(() => translator.Translate(ast, "Employees"));
    }

    [Fact]
    public void Translate_WithDistinct_BuildsSelectDistinct()
    {
        var translator = new SqlServerSqlTranslator();
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
            "SELECT DISTINCT TOP (10) [t].[City] AS [City], [t].[Department] AS [Department] FROM [Employees] AS [t]",
            result.Sql);
        Assert.Contains("ORDER BY [t].[City] ASC", result.Sql);
    }

    [Fact]
    public void Translate_WithNestedField_UsesJsonValue()
    {
        var translator = new SqlServerSqlTranslator();
        var ast = new QueryAst(
            new FieldFilter(new FieldPath("Address.City"), [new ScalarOperation(FieldOperator.Eq, new ScalarValue("Delhi"))]),
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            null);

        var result = translator.Translate(ast, "Employees");

        Assert.Contains("JSON_VALUE([t].[Address], '$.City') = @p0", result.Sql);
        Assert.Equal("Delhi", result.Parameters["@p0"]);
    }

    [Fact]
    public void Translate_WithSize_UsesOpenJsonCount()
    {
        var translator = new SqlServerSqlTranslator();
        var ast = new QueryAst(
            new FieldFilter(new FieldPath("Tags"), [new IntegerOperation(FieldOperator.Size, 2)]),
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            null);

        var result = translator.Translate(ast, "Employees");

        Assert.Contains("(SELECT COUNT(1) FROM OPENJSON([t].[Tags])) = @p0", result.Sql);
        Assert.Equal(2, result.Parameters["@p0"]);
    }

    [Fact]
    public void Translate_WithContainsAny_UsesOpenJsonExists()
    {
        var translator = new SqlServerSqlTranslator();
        var ast = new QueryAst(
            new FieldFilter(
                new FieldPath("Tags"),
                [new ListOperation(FieldOperator.ContainsAny, [new ScalarValue("a"), new ScalarValue("b")])]),
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            null);

        var result = translator.Translate(ast, "Employees");

        Assert.Contains("OPENJSON([t].[Tags])", result.Sql);
        Assert.Contains("OR", result.Sql);
        Assert.Equal("a", result.Parameters["@p0"]);
        Assert.Equal("b", result.Parameters["@p1"]);
    }

    [Fact]
    public void Translate_WithContainsAll_UsesOpenJsonExistsAnd()
    {
        var translator = new SqlServerSqlTranslator();
        var ast = new QueryAst(
            new FieldFilter(
                new FieldPath("Skills"),
                [new ListOperation(FieldOperator.ContainsAll, [new ScalarValue("Azure"), new ScalarValue(".NET")])]),
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            null);

        var result = translator.Translate(ast, "Employees");

        Assert.Contains("OPENJSON([t].[Skills])", result.Sql);
        Assert.Contains("AND", result.Sql);
        Assert.Equal("Azure", result.Parameters["@p0"]);
        Assert.Equal(".NET", result.Parameters["@p1"]);
    }

    [Fact]
    public void Translate_WithIsEmpty_UsesOpenJsonExists()
    {
        var translator = new SqlServerSqlTranslator();
        var ast = new QueryAst(
            new FieldFilter(new FieldPath("Tags"), [new BooleanOperation(FieldOperator.IsEmpty, true)]),
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            null);

        var result = translator.Translate(ast, "Employees");

        Assert.Contains("NOT EXISTS (SELECT 1 FROM OPENJSON([t].[Tags]))", result.Sql);
    }

    [Fact]
    public void Translate_WithAny_UsesOpenJsonValuePredicate()
    {
        var translator = new SqlServerSqlTranslator();
        var predicate = new LogicalFilter(
            FilterLogicalOperator.And,
            [
                new FieldFilter(new FieldPath("Status"), [new ScalarOperation(FieldOperator.Eq, new ScalarValue("Active"))]),
                new FieldFilter(new FieldPath("Hours"), [new ScalarOperation(FieldOperator.Gt, new ScalarValue(20))])
            ]);
        var ast = new QueryAst(
            new FieldFilter(new FieldPath("Projects"), [new AnyOperation(predicate)]),
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            null);

        var result = translator.Translate(ast, "Employees");

        Assert.Contains("OPENJSON([t].[Projects]) WITH ([value] NVARCHAR(MAX) '$' AS JSON)", result.Sql);
        Assert.Contains("JSON_VALUE([a0].[value], '$.Status') = @p0", result.Sql);
        Assert.Contains("JSON_VALUE([a0].[value], '$.Hours') > @p1", result.Sql);
        Assert.Equal("Active", result.Parameters["@p0"]);
        Assert.Equal(20, result.Parameters["@p1"]);
    }

    [Fact]
    public void Translate_WithRegex_ThrowsNotSupportedException()
    {
        var translator = new SqlServerSqlTranslator();
        var ast = new QueryAst(
            new FieldFilter(new FieldPath("Name"), [new ScalarOperation(FieldOperator.Regex, new ScalarValue("Ada.*"))]),
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            null);

        var ex = Assert.Throws<NotSupportedException>(() => translator.Translate(ast, "Employees"));
        Assert.Contains("regex", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Translate_WithEmptyTableName_ThrowsArgumentException()
    {
        var translator = new SqlServerSqlTranslator();
        var ast = new QueryAst(
            null,
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            null);

        Assert.Throws<ArgumentException>(() => translator.Translate(ast, "  "));
    }

    [Fact]
    public void Translate_WithGroupedLimit_UsesFetchNext()
    {
        var translator = new SqlServerSqlTranslator();
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
        Assert.Contains("GROUP BY [t].[Department]", result.Sql);
        Assert.Contains("FETCH NEXT 5 ROWS ONLY", result.Sql);
    }
}
