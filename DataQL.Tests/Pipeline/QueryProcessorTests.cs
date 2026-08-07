using System.Text.Json;
using DataQL.Ast.Model;
using DataQL.Ast.Parsing;
using DataQL.Contracts;
using DataQL.Pipeline;
using DataQL.Validation;

namespace DataQL.Tests.Pipeline;

public class QueryProcessorTests
{
    [Fact]
    public void Process_WithValidRequest_ReturnsAst()
    {
        var processor = new QueryProcessor(
            new QueryRequestValidator(),
            new QueryAstParser(),
            new AstSemanticValidator());

        var request = new QueryRequest
        {
            Where = JsonDocument.Parse("{\"age\":{\"$gte\":18}}").RootElement,
            Order = [new OrderClause { Field = "age", Direction = "asc" }],
            Limit = 10
        };

        var ast = processor.Process(request);

        Assert.NotNull(ast);
        Assert.NotNull(ast.Where);
        Assert.Single(ast.Order);
    }

    [Fact]
    public void Process_WithInvalidRequest_ThrowsAstValidationException()
    {
        var processor = new QueryProcessor(
            new QueryRequestValidator(),
            new QueryAstParser(),
            new AstSemanticValidator());

        var request = new QueryRequest
        {
            Limit = 0
        };

        var ex = Assert.Throws<AstValidationException>(() => processor.Process(request));

        Assert.Contains(ex.Errors, e => e.Code == "Limit.OutOfRange");
    }

    [Fact]
    public void Process_WithContinuationTokenAndNoLimit_ThrowsAstValidationException()
    {
        var processor = new QueryProcessor(
            new QueryRequestValidator(),
            new QueryAstParser(),
            new AstSemanticValidator());

        var request = new QueryRequest
        {
            ContinuationToken = "abc",
            Order = [new OrderClause { Field = "age", Direction = "asc" }]
        };

        var ex = Assert.Throws<AstValidationException>(() => processor.Process(request));
        Assert.Contains(ex.Errors, e => e.Code == "ContinuationToken.RequiresLimit");
    }

    [Fact]
    public void Process_WithInvalidAst_ThrowsAstValidationException()
    {
        var processor = new QueryProcessor(
            new QueryRequestValidator(),
            new QueryAstParser(),
            new AstSemanticValidator());

        var request = new QueryRequest
        {
            Where = JsonDocument.Parse("{\"skills\":{\"$in\":\"Azure\"}}").RootElement,
            Order = [new OrderClause { Field = "age", Direction = "asc" }]
        };

        var ex = Assert.Throws<AstParseException>(() => processor.Process(request));

        Assert.Contains("array value", ex.Message);
    }
}
