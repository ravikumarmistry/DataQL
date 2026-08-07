using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using DataQL.SqlServer.Translation;

namespace DataQL.SqlServer.Execution;

public interface ISqlServerQueryExecutor
{
    Task<IReadOnlyList<T>> ExecuteRowsAsync<T>(
        IDbConnection connection,
        SqlServerSqlTranslationResult translation,
        CancellationToken cancellationToken = default);

    Task<long> ExecuteCountAsync(
        IDbConnection connection,
        SqlServerSqlTranslationResult translation,
        CancellationToken cancellationToken = default);
}