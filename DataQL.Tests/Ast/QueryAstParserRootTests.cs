using System.Text.Json;
using DataQL.Ast.Model;
using DataQL.Ast.Parsing;
using DataQL.Contracts;

namespace DataQL.Tests.Ast;

public class QueryAstParserRootTests
{
    [Fact]
    public void Parse_MapsProjectionOrderingAndPagination()
    {
        var parser = new QueryAstParser();
        var request = new QueryRequest
        {
            Select = ["name", "address.city"],
            Exclude = ["internal.notes"],
            Distinct = ["city"],
            Order = [new OrderClause { Field = "createdAt", Direction = "desc" }],
            Limit = 50,
            ContinuationToken = "token",
            IncludeCount = true,
            Where = JsonDocument.Parse("{\"city\":\"Delhi\"}").RootElement
        };

        var ast = parser.Parse(request);

        Assert.Equal(2, ast.Projection.Select.Count);
        Assert.Single(ast.Projection.Exclude);
        Assert.Single(ast.Projection.Distinct);
        Assert.Single(ast.Order);
        Assert.Equal(SortDirection.Desc, ast.Order[0].Direction);
        Assert.Equal(50, ast.Pagination.Limit);
        Assert.Equal("token", ast.Pagination.ContinuationToken);
        Assert.True(ast.Pagination.IncludeCount);
        Assert.True(ast.Pagination.RequiresDeterministicOrder);
    }

    [Fact]
    public void Parse_WithInvalidOperator_ThrowsAstParseException()
    {
        var parser = new QueryAstParser();
        var request = new QueryRequest
        {
            Where = JsonDocument.Parse("{\"age\":{\"$unknown\":123}}").RootElement
        };

        var ex = Assert.Throws<AstParseException>(() => parser.Parse(request));

        Assert.Contains("Unknown operator", ex.Message);
    }

    [Fact]
    public void Parse_WithWildcardFieldOnNonCountMetric_ThrowsAstParseException()
    {
        var parser = new QueryAstParser();
        var request = new QueryRequest
        {
            Group = new GroupRequest
            {
                GroupBy = ["department"],
                Metrics =
                [
                    new GroupMetricRequest { Field = "*", Operation = "sum", Alias = "total" }
                ]
            }
        };

        Assert.Throws<AstParseException>(() => parser.Parse(request));
    }
}
