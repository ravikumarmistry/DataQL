using System.Text.Json;
using DataQL.Ast.Model;
using DataQL.Ast.Parsing;
using DataQL.Contracts;

namespace DataQL.Tests.Ast;

public class QueryAstParserFilterTests
{
    private readonly QueryAstParser _parser = new();

    [Fact]
    public void Parse_WithImplicitEquality_CreatesEqOperation()
    {
        var request = new QueryRequest
        {
            Where = JsonDocument.Parse("{" +
                "\"city\":\"Delhi\"" +
            "}").RootElement
        };

        var ast = _parser.Parse(request);

        var field = Assert.IsType<FieldFilter>(ast.Where);
        Assert.Equal("city", field.Field.Value);
        var op = Assert.IsType<ScalarOperation>(Assert.Single(field.Operations));
        Assert.Equal(FieldOperator.Eq, op.Operator);
        Assert.Equal("Delhi", Assert.IsType<ScalarValue>(op.Value).Value);
    }

    [Fact]
    public void Parse_WithLogicalAndOrNot_CreatesNestedLogicalAst()
    {
        var request = new QueryRequest
        {
            Where = JsonDocument.Parse("{" +
                "\"$and\":[{" +
                    "\"$or\":[{\"city\":\"Delhi\"},{\"city\":\"Mumbai\"}]" +
                "},{" +
                    "\"$not\":{\"active\":true}" +
                "}]" +
            "}").RootElement
        };

        var ast = _parser.Parse(request);

        var and = Assert.IsType<LogicalFilter>(ast.Where);
        Assert.Equal(FilterLogicalOperator.And, and.Operator);
        Assert.Equal(2, and.Children.Count);
    }

    [Fact]
    public void Parse_WithArrayAndStringOperators_CreatesOperations()
    {
        var request = new QueryRequest
        {
            Where = JsonDocument.Parse("{" +
                "\"skills\":{\"$containsAll\":[\"Azure\",\".NET\"]}," +
                "\"name\":{\"$startsWith\":\"Jo\"}" +
            "}").RootElement
        };

        var ast = _parser.Parse(request);

        var root = Assert.IsType<LogicalFilter>(ast.Where);
        Assert.Equal(2, root.Children.Count);
    }

    [Fact]
    public void Parse_WithAnyOperator_CreatesAnyOperation()
    {
        var request = new QueryRequest
        {
            Where = JsonDocument.Parse("{" +
                "\"projects\":{\"$any\":{\"status\":\"Active\",\"hours\":{\"$gt\":20}}}" +
            "}").RootElement
        };

        var ast = _parser.Parse(request);

        var field = Assert.IsType<FieldFilter>(ast.Where);
        var any = Assert.IsType<AnyOperation>(Assert.Single(field.Operations));
        Assert.NotNull(any.Predicate);
    }
}
