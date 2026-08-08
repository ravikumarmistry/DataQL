using System;
using System.Threading.Tasks;
using DataQL.Abstractions;
using DataQL.Cosmos.Execution;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DataQL.Cosmos.DependencyInjection;

public static class CosmosDataQLOptionsExtensions
{
    public static DataQLOptions AddCosmosSource(
        this DataQLOptions options,
        string sourceKey,
        Func<IServiceProvider, CosmosClient> clientFactory,
        string databaseId)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            throw new ArgumentException("Source key is required.", nameof(sourceKey));
        }

        if (clientFactory is null)
        {
            throw new ArgumentNullException(nameof(clientFactory));
        }

        if (string.IsNullOrWhiteSpace(databaseId))
        {
            throw new ArgumentException("Database id is required.", nameof(databaseId));
        }

        options.Services.TryAddSingleton<CosmosQueryExecutionEngine>();
        options.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IDataQLProviderExecutor, CosmosDataQLProviderExecutor>());

        var db = databaseId.Trim();
        return options.AddSource(
            sourceKey,
            new DataQLSourceRegistration(
                ProviderName.Cosmos,
                sp => new ValueTask<IDataQLSession>(
                    new CosmosDataQLSession(clientFactory(sp), db, ownsClient: false))));
    }

    public static DataQLOptions AddCosmosSource(
        this DataQLOptions options,
        string sourceKey,
        string endpoint,
        string key,
        string databaseId)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException("Endpoint is required.", nameof(endpoint));
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key is required.", nameof(key));
        }

        var client = new CosmosClient(endpoint.Trim(), key.Trim(), new CosmosClientOptions
        {
            ConnectionMode = ConnectionMode.Gateway,
            LimitToEndpoint = true
        });

        return options.AddCosmosSource(sourceKey, _ => client, databaseId);
    }
}
