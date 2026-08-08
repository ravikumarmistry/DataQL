using System;
using System.Collections.Generic;
using DataQL.Abstractions;
using DataQL.Ast.Model;
using DataQL.Contracts;
using DataQL.Validation;

namespace DataQL.Cosmos.Validation;

public sealed class CosmosProviderQueryValidator : IProviderQueryValidator
{
    public static CosmosProviderQueryValidator Instance { get; } = new();

    public string Provider => ProviderName.Cosmos;

    public ValidationResult Validate(QueryAst ast, QueryRequest request, ProviderCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(ast);
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<ValidationError>();
        var hasGroup = ast.Group is not null || request.Group is not null;

        if (hasGroup && !string.IsNullOrWhiteSpace(request.ContinuationToken))
        {
            errors.Add(new ValidationError(
                "continuationToken",
                "Provider.Group.ContinuationNotSupported",
                "Cosmos grouped queries do not support continuation tokens.",
                Provider));
        }

        if (hasGroup && request.IncludeCount)
        {
            errors.Add(new ValidationError(
                "includeCount",
                "Provider.Group.IncludeCountNotSupported",
                "Cosmos grouped queries do not support includeCount.",
                Provider));
        }

        return errors.Count == 0 ? ValidationResult.Success() : new ValidationResult(errors);
    }
}
