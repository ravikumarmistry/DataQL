using System;
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
    private readonly ProviderCapabilities _capabilities = new DataQL.Sqlite.SqliteQueryTranslator().Capabilities;

    public string Provider => ProviderName.Sqlite;

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
                $"Sqlite executor requires {nameof(AdoDataQLSession)}, but received '{session?.GetType().Name ?? "null"}'.");
        }

        return ado.Connection;
    }
}
