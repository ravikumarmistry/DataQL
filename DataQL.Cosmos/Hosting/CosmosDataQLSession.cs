using System;
using System.Threading.Tasks;
using DataQL.Abstractions;
using Microsoft.Azure.Cosmos;

namespace DataQL.Cosmos;

public sealed class CosmosDataQLSession : IDataQLSession
{
    private readonly bool _ownsClient;
    private bool _disposed;

    public CosmosDataQLSession(CosmosClient client, string databaseId, bool ownsClient = false)
    {
        Client = client ?? throw new ArgumentNullException(nameof(client));
        if (string.IsNullOrWhiteSpace(databaseId))
        {
            throw new ArgumentException("Database id is required.", nameof(databaseId));
        }

        DatabaseId = databaseId.Trim();
        _ownsClient = ownsClient;
    }

    public string Provider => ProviderName.Cosmos;

    public CosmosClient Client { get; }

    public string DatabaseId { get; }

    public Container GetContainer(string containerId)
    {
        if (string.IsNullOrWhiteSpace(containerId))
        {
            throw new ArgumentException("Container id is required.", nameof(containerId));
        }

        return Client.GetContainer(DatabaseId, containerId.Trim());
    }

    public Database GetDatabase() => Client.GetDatabase(DatabaseId);

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        if (_ownsClient)
        {
            Client.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
