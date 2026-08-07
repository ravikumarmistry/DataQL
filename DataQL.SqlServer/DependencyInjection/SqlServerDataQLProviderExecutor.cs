using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using DataQL.Abstractions;
using DataQL.Contracts;
using DataQL.SqlServer.Execution;
using DataQL.SqlServer.Metadata;

namespace DataQL.SqlServer.DependencyInjection;

public sealed class SqlServerDataQLProviderExecutor(
    SqlServerQueryExecutionEngine engine) : IDataQLProviderExecutor
{
    private readonly SqlServerQueryExecutionEngine _engine = engine;
    private readonly SqlServerMetadataProvider _metadata = new();

    public string Provider => ProviderName.SqlServer;

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
