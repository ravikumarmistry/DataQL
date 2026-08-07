using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using DataQL.Abstractions;
using DataQL.Contracts;
using DataQL.Sqlite.Execution;
using DataQL.Sqlite.Metadata;

namespace DataQL.Sqlite.DependencyInjection;

public sealed class SqliteDataQLProviderExecutor(
    SqliteQueryExecutionEngine engine) : IDataQLProviderExecutor
{
    private readonly SqliteQueryExecutionEngine _engine = engine;
    private readonly SqliteMetadataProvider _metadata = new();

    public string Provider => ProviderName.Sqlite;

    public Task<QueryResponse<T>> ExecuteAsync<T>(
        IDbConnection connection,
        QuerySource source,
        QueryRequest request,
        CancellationToken cancellationToken = default)
    {
        return _engine.ExecuteAsync<T>(connection, source, request, cancellationToken);
    }

    public Task<IReadOnlyList<DataQLTableInfo>> ListTablesAsync(
        IDbConnection connection,
        CancellationToken cancellationToken = default)
    {
        return _metadata.ListTablesAsync(connection, cancellationToken);
    }

    public Task<DataQLTableSchema> GetTableSchemaAsync(
        IDbConnection connection,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        return _metadata.GetTableSchemaAsync(connection, tableName, cancellationToken);
    }
}
