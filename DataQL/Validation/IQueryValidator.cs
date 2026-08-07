using DataQL.Contracts;

namespace DataQL.Validation;

public interface IQueryValidator
{
    ValidationResult Validate(QueryRequest request);
}
