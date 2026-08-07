using System.Collections.Generic;

namespace DataQL.SqlServer.Translation;

public sealed class SqlServerSqlTranslationResult
{
    public string Sql { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, object?> Parameters { get; init; } = new Dictionary<string, object?>();
}
