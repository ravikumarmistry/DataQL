using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataQL.Ast.Parsing;
using DataQL.Validation;
using Microsoft.AspNetCore.Http;

namespace DataQL.AspNetCore;

internal static class DataQLHttpResults
{
    public static IResult ValidationProblem(IReadOnlyList<ValidationError> errors)
        => Results.Json(new DataQLErrorResponse(errors), statusCode: StatusCodes.Status400BadRequest);

    public static IResult FromParseException(AstParseException ex)
        => ValidationProblem(
        [
            new ValidationError(ex.Path, "Parse.Invalid", ex.Message)
        ]);

    public static async Task<IResult> ExecuteAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (AstValidationException ex)
        {
            return ValidationProblem(ex.Errors);
        }
        catch (AstParseException ex)
        {
            return FromParseException(ex);
        }
    }
}

public sealed class DataQLErrorResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("errors")]
    public IReadOnlyList<ValidationError> Errors { get; }

    public DataQLErrorResponse(IReadOnlyList<ValidationError> errors)
    {
        Errors = errors;
    }
}
