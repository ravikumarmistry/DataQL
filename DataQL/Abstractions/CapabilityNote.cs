namespace DataQL.Abstractions;

public sealed class CapabilityNote
{
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// One of: info, warning, restriction.
    /// </summary>
    public string Severity { get; init; } = "info";

    public string Message { get; init; } = string.Empty;
}
