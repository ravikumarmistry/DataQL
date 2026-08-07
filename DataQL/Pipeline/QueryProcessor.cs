using DataQL.Ast.Model;
using DataQL.Ast.Parsing;
using DataQL.Contracts;
using DataQL.Validation;

namespace DataQL.Pipeline;

public sealed class QueryProcessor
{
    private readonly IQueryValidator _requestValidator;
    private readonly QueryAstParser _parser;
    private readonly IAstValidator _astValidator;

    public QueryProcessor(IQueryValidator requestValidator, QueryAstParser parser, IAstValidator astValidator)
    {
        _requestValidator = requestValidator;
        _parser = parser;
        _astValidator = astValidator;
    }

    public QueryAst Process(QueryRequest request)
    {
        var requestValidation = _requestValidator.Validate(request);
        if (!requestValidation.IsValid)
        {
            throw new AstValidationException(requestValidation.Errors);
        }

        var ast = _parser.Parse(request);
        var astValidation = _astValidator.Validate(ast);
        if (!astValidation.IsValid)
        {
            throw new AstValidationException(astValidation.Errors);
        }

        return ast;
    }
}
