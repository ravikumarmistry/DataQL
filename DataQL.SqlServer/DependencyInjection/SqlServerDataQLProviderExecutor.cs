using System;
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
    private readonly ProviderCapabilities _capabilities = new DataQL.SqlServer.SqlServerQueryTranslator().Capabilities;

    public string Provider => ProviderName.SqlServer;

    public ProviderCapabilities Capabilities => _capabilities;

    public Task<QueryResponse<T>> ExecuteAsync<T>(
        IDataQLSession session,
        QuerySource source,
        QueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var connection = RequireAdoConnection(session);
        return _engine.ExecuteAsync<T>(connection, source, request, cancellationToken);
    }

    public Task<IReadOnlyList<DataQLTableInfo>> ListTablesAsync(
        IDataQLSession session,
        CancellationToken cancellationToken = default)
    {
        var connection = RequireAdoConnection(session);
        return _metadata.ListTablesAsync(connection, cancellationToken);
    }

    public Task<DataQLTableSchema> GetTableSchemaAsync(
        IDataQLSession session,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        var connection = RequireAdoConnection(session);
        return _metadata.GetTableSchemaAsync(connection, tableName, cancellationToken);
    }

    private static IDbConnection RequireAdoConnection(IDataQLSession session)
    {
        if (session is not AdoDataQLSession ado)
        {
            throw new InvalidOperationException(
                $"SqlServer executor requires {nameof(AdoDataQLSession)}, but received '{session?.GetType().Name ?? "null"}'.");
        }

        return ado.Connection;
    }
}
