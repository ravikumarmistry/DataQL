using System;
using System.Collections.Generic;

namespace DataQL.Validation;

public sealed class AstValidationException : Exception
{
    public IReadOnlyList<ValidationError> Errors { get; }

    public AstValidationException(IReadOnlyList<ValidationError> errors)
        : base("Query validation failed.")
    {
        Errors = errors;
    }
}
