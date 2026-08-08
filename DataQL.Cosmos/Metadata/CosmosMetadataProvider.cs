using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DataQL.Abstractions;
using DataQL.Contracts;
using Microsoft.Azure.Cosmos;

namespace DataQL.Cosmos.Metadata;

public sealed class CosmosMetadataProvider
{
    public async Task<IReadOnlyList<DataQLTableInfo>> ListTablesAsync(
        CosmosDataQLSession session,
        CancellationToken cancellationToken = default)
    {
        var database = session.GetDatabase();
        var tables = new List<DataQLTableInfo>();

        using var iterator = database.GetContainerQueryIterator<ContainerProperties>("SELECT * FROM c");
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            foreach (var container in page)
            {
                tables.Add(new DataQLTableInfo(container.Id, Schema: null));
            }
        }

        return tables;
    }

    public async Task<DataQLTableSchema> GetTableSchemaAsync(
        CosmosDataQLSession session,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        var containerId = tableName.Trim();
        var database = session.GetDatabase();

        try
        {
            var container = database.GetContainer(containerId);
            await container.ReadContainerAsync(cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException($"Container '{containerId}' was not found.", ex);
        }

        using var schema = JsonDocument.Parse("""{"type":"object","additionalProperties":true}""");
        return new DataQLTableSchema
        {
            SourceKey = string.Empty,
            Table = containerId,
            Provider = ProviderName.Cosmos,
            Schema = schema.RootElement.Clone()
        };
    }
}
