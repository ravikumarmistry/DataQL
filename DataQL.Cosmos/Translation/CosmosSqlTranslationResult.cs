using System.Collections.Generic;

namespace DataQL.Cosmos.Translation;

public sealed class CosmosSqlTranslationResult
{
    public string Sql { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, object?> Parameters { get; init; } = new Dictionary<string, object?>();

    /// <summary>
    /// Grouped queries cannot use Cosmos feed continuation tokens.
    /// </summary>
    public bool IsGrouped { get; init; }
}
