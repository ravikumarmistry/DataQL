using System;
using DataQL.Abstractions;
using DataQL.Ast.Model;
using DataQL.Contracts;

namespace DataQL.Validation;

public interface IProviderQueryValidator
{
    string Provider { get; }

    ValidationResult Validate(QueryAst ast, QueryRequest request, ProviderCapabilities capabilities);
}

public static class ProviderQueryValidatorExtensions
{
    public static void EnsureValid(
        this IProviderQueryValidator validator,
        QueryAst ast,
        QueryRequest request,
        ProviderCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(validator);

        var result = validator.Validate(ast, request, capabilities);
        if (!result.IsValid)
        {
            throw new AstValidationException(result.Errors);
        }
    }
}
