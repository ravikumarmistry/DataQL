using System.Collections.Generic;

namespace DataQL.Sqlite.Translation;

public sealed class SqliteSqlTranslationResult
{
    public string Sql { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, object?> Parameters { get; init; } = new Dictionary<string, object?>();
}
