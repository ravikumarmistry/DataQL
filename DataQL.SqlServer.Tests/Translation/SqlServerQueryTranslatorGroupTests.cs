using DataQL.Ast.Model;
using DataQL.Abstractions;
using DataQL.SqlServer.Translation;

namespace DataQL.SqlServer.Tests.Translation;

public class SqlServerQueryTranslatorGroupTests
{
    [Fact]
    public void Translate_WithGroup_ReturnsSqlServerTranslationResult()
    {
        var translator = new DataQL.SqlServer.SqlServerQueryTranslator();
        var ast = new QueryAst(
            null,
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            new GroupAst(
                [new FieldPath("Department")],
                [new GroupMetricAst(new FieldPath("*"), GroupMetricOperation.Count, "employees")]));

        var source = new QuerySource(ProviderName.SqlServer, "Employees");
        var result = Assert.IsType<SqlServerSqlTranslationResult>(translator.Translate(ast, source));

        Assert.Contains("GROUP BY", result.Sql);
        Assert.Contains("COUNT(1)", result.Sql);
    }
}
