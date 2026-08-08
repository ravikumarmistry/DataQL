using DataQL.Abstractions;
using DataQL.Ast.Model;
using DataQL.Sqlite.Translation;

namespace DataQL.Sqlite.Tests.Translation;

public class SqliteQueryTranslatorGroupTests
{
    [Fact]
    public void Translate_WithGroup_ReturnsSqliteTranslationResult()
    {
        var translator = new DataQL.Sqlite.SqliteQueryTranslator();
        var ast = new QueryAst(
            null,
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            new GroupAst(
                [new FieldPath("Department")],
                [new GroupMetricAst(new FieldPath("*"), GroupMetricOperation.Count, "employees")])) ;

        var source = new QuerySource(ProviderName.Sqlite, "Employees");
        var result = Assert.IsType<SqliteSqlTranslationResult>(translator.Translate(ast, source));

        Assert.Contains("GROUP BY", result.Sql);
        Assert.Contains("COUNT(1)", result.Sql);
    }

    [Fact]
    public void Translate_WithWrongProvider_ThrowsArgumentException()
    {
        var translator = new DataQL.Sqlite.SqliteQueryTranslator();
        var ast = new QueryAst(
            null,
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            null);

        var source = new QuerySource(ProviderName.SqlServer, "Employees");
        var ex = Assert.Throws<ArgumentException>(() => translator.Translate(ast, source));
        Assert.Contains("not valid", ex.Message);
    }

    [Fact]
    public void Translate_WithEmptySourceName_ThrowsArgumentException()
    {
        var translator = new DataQL.Sqlite.SqliteQueryTranslator();
        var ast = new QueryAst(
            null,
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            null);

        var source = new QuerySource(ProviderName.Sqlite, " ");
        Assert.Throws<ArgumentException>(() => translator.Translate(ast, source));
    }
}
