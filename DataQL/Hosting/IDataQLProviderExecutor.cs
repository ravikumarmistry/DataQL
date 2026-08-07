using System.Data;
using System.Threading;
using System.Threading.Tasks;
using DataQL.Abstractions;
using DataQL.Contracts;

namespace DataQL;

public interface IDataQLProviderExecutor
{
    string Provider { get; }

    Task<QueryResponse<T>> ExecuteAsync<T>(
        IDbConnection connection,
        QuerySource source,
        QueryRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DataQLTableInfo>> ListTablesAsync(
        IDbConnection connection,
        CancellationToken cancellationToken = default);

    Task<DataQLTableSchema> GetTableSchemaAsync(
        IDbConnection connection,
        string tableName,
        CancellationToken cancellationToken = default);
}
