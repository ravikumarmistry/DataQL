namespace DataQL.Token;

public sealed class ContinuationTokenEnvelope
{
    public string Provider { get; init; } = string.Empty;
    public string QueryShapeHash { get; init; } = string.Empty;
    public string ProviderToken { get; init; } = string.Empty;
}
