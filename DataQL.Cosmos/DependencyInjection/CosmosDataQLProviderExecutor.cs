using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataQL.Abstractions;
using DataQL.Contracts;
using DataQL.Cosmos.Execution;
using DataQL.Cosmos.Metadata;

namespace DataQL.Cosmos.DependencyInjection;

public sealed class CosmosDataQLProviderExecutor(
    CosmosQueryExecutionEngine engine) : IDataQLProviderExecutor
{
    private readonly CosmosQueryExecutionEngine _engine = engine;
    private readonly CosmosMetadataProvider _metadata = new();

    public string Provider => ProviderName.Cosmos;

    public Task<QueryResponse<T>> ExecuteAsync<T>(
        IDataQLSession session,
        QuerySource source,
        QueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var cosmosSession = RequireCosmosSession(session);
        return _engine.ExecuteAsync<T>(cosmosSession, source, request, cancellationToken);
    }

    public Task<IReadOnlyList<DataQLTableInfo>> ListTablesAsync(
        IDataQLSession session,
        CancellationToken cancellationToken = default)
    {
        var cosmosSession = RequireCosmosSession(session);
        return _metadata.ListTablesAsync(cosmosSession, cancellationToken);
    }

    public Task<DataQLTableSchema> GetTableSchemaAsync(
        IDataQLSession session,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        var cosmosSession = RequireCosmosSession(session);
        return _metadata.GetTableSchemaAsync(cosmosSession, tableName, cancellationToken);
    }

    private static CosmosDataQLSession RequireCosmosSession(IDataQLSession session)
    {
        if (session is not CosmosDataQLSession cosmos)
        {
            throw new InvalidOperationException(
                $"Cosmos executor requires {nameof(CosmosDataQLSession)}, but received '{session?.GetType().Name ?? "null"}'.");
        }

        return cosmos;
    }
}
