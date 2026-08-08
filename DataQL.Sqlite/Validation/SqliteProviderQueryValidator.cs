using System;
using System.Collections.Generic;
using DataQL.Abstractions;
using DataQL.Ast.Model;
using DataQL.Contracts;
using DataQL.Validation;

namespace DataQL.Sqlite.Validation;

public sealed class SqliteProviderQueryValidator : IProviderQueryValidator
{
    public static SqliteProviderQueryValidator Instance { get; } = new();

    public string Provider => ProviderName.Sqlite;

    public ValidationResult Validate(QueryAst ast, QueryRequest request, ProviderCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(ast);
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<ValidationError>();
        if (request.Order.Count == 0)
        {
            errors.Add(new ValidationError(
                "order",
                "Order.Required",
                "order must contain at least one sort field.",
                Provider));
        }

        return errors.Count == 0 ? ValidationResult.Success() : new ValidationResult(errors);
    }
}
