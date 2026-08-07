using DataQL.Ast.Model;
using DataQL.Ast.Parsing;
using DataQL.Contracts;

namespace DataQL.Tests.Ast;

public class QueryAstParserGroupTests
{
    private readonly QueryAstParser _parser = new();

    [Fact]
    public void Parse_WithGroupDefinition_BuildsGroupAst()
    {
        var request = new QueryRequest
        {
            Group = new GroupRequest
            {
                GroupBy = ["department", "city"],
                Metrics =
                [
                    new GroupMetricRequest { Field = "salary", Operation = "avg", Alias = "averageSalary" },
                    new GroupMetricRequest { Field = "salary", Operation = "sum", Alias = "totalSalary" },
                    new GroupMetricRequest { Field = "*", Operation = "count", Alias = "employees" }
                ]
            }
        };

        var ast = _parser.Parse(request);

        Assert.NotNull(ast.Group);
        Assert.Equal(2, ast.Group!.GroupBy.Count);
        Assert.Equal("department", ast.Group.GroupBy[0].Value);
        Assert.Equal("city", ast.Group.GroupBy[1].Value);
        Assert.Equal(3, ast.Group.Metrics.Count);
        Assert.Equal(GroupMetricOperation.Avg, ast.Group.Metrics[0].Operation);
        Assert.Equal(GroupMetricOperation.Sum, ast.Group.Metrics[1].Operation);
        Assert.Equal(GroupMetricOperation.Count, ast.Group.Metrics[2].Operation);
        Assert.Null(ast.Group.Having);
    }

    [Fact]
    public void Parse_WithGroupHaving_BuildsHavingFilter()
    {
        var request = new QueryRequest
        {
            Group = new GroupRequest
            {
                GroupBy = ["Department"],
                Metrics =
                [
                    new GroupMetricRequest { Field = "*", Operation = "count", Alias = "employees" }
                ],
                Having = QueryFilterBuilder.Field("employees").Gte(2).ToJsonElement()
            }
        };

        var ast = _parser.Parse(request);

        Assert.NotNull(ast.Group);
        Assert.NotNull(ast.Group!.Having);
        var field = Assert.IsType<FieldFilter>(ast.Group.Having);
        Assert.Equal("employees", field.Field.Value);
        var op = Assert.IsType<ScalarOperation>(Assert.Single(field.Operations));
        Assert.Equal(FieldOperator.Gte, op.Operator);
    }

    [Fact]
    public void Parse_WithUnsupportedMetricOperation_ThrowsAstParseException()
    {
        var request = new QueryRequest
        {
            Group = new GroupRequest
            {
                GroupBy = ["department"],
                Metrics =
                [
                    new GroupMetricRequest { Field = "salary", Operation = "median", Alias = "medianSalary" }
                ]
            }
        };

        var ex = Assert.Throws<AstParseException>(() => _parser.Parse(request));

        Assert.Contains("Unsupported metric operation", ex.Message);
    }
}
