using System.Threading;
using System.Threading.Tasks;
using DataQL.Abstractions;
using DataQL.Contracts;

namespace DataQL;

public interface IDataQLProviderExecutor
{
    string Provider { get; }

    Task<QueryResponse<T>> ExecuteAsync<T>(
        IDataQLSession session,
        QuerySource source,
        QueryRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DataQLTableInfo>> ListTablesAsync(
        IDataQLSession session,
        CancellationToken cancellationToken = default);

    Task<DataQLTableSchema> GetTableSchemaAsync(
        IDataQLSession session,
        string tableName,
        CancellationToken cancellationToken = default);
}
