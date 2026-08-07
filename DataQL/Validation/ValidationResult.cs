using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DataQL.Validation;

public sealed class ValidationResult
{
    public IReadOnlyList<ValidationError> Errors { get; }

    public bool IsValid => Errors.Count == 0;

    public ValidationResult(IReadOnlyList<ValidationError> errors)
    {
        Errors = errors;
    }

    public static ValidationResult Success() => new([]);
}

public sealed class ValidationError
{
    [JsonPropertyName("path")]
    public string Path { get; }

    [JsonPropertyName("code")]
    public string Code { get; }

    [JsonPropertyName("message")]
    public string Message { get; }

    [JsonPropertyName("provider")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Provider { get; }

    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, object?>? Details { get; }

    public ValidationError(
        string path,
        string code,
        string message,
        string? provider = null,
        IReadOnlyDictionary<string, object?>? details = null)
    {
        Path = path;
        Code = code;
        Message = message;
        Provider = provider;
        Details = details;
    }
}
