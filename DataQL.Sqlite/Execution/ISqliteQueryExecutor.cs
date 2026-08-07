using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using DataQL.Sqlite.Translation;

namespace DataQL.Sqlite.Execution;

public interface ISqliteQueryExecutor
{
    Task<IReadOnlyList<T>> ExecuteRowsAsync<T>(
        IDbConnection connection,
        SqliteSqlTranslationResult translation,
        CancellationToken cancellationToken = default);

    Task<long> ExecuteCountAsync(
        IDbConnection connection,
        SqliteSqlTranslationResult translation,
        CancellationToken cancellationToken = default);
}
