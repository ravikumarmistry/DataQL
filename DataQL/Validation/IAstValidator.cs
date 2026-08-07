using DataQL.Ast.Model;

namespace DataQL.Validation;

public interface IAstValidator
{
    ValidationResult Validate(QueryAst ast);
}
