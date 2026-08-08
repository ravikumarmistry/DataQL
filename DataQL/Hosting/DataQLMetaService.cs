using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataQL.Contracts;

namespace DataQL;

public sealed class DataQLMetaService : IDataQLMetaService
{
    private readonly DataQLOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadOnlyDictionary<string, IDataQLProviderExecutor> _executors;

    public DataQLMetaService(
        DataQLOptions options,
        IServiceProvider serviceProvider,
        IEnumerable<IDataQLProviderExecutor> executors)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        var map = new Dictionary<string, IDataQLProviderExecutor>(StringComparer.OrdinalIgnoreCase);
        foreach (var executor in executors)
        {
            map[executor.Provider] = executor;
        }

        _executors = map;
    }

    public Task<IReadOnlyList<DataQLSourceInfo>> ListSourcesAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<DataQLSourceInfo> sources = _options.Sources
            .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static pair => new DataQLSourceInfo(pair.Key, pair.Value.Provider))
            .ToList();

        return Task.FromResult(sources);
    }

    public async Task<IReadOnlyList<DataQLTableInfo>> ListTablesAsync(
        string sourceKey,
        CancellationToken cancellationToken = default)
    {
        var (registration, executor) = Resolve(sourceKey);

        await using var session = await registration.SessionFactory(_serviceProvider);
        return await executor.ListTablesAsync(session, cancellationToken);
    }

    public async Task<DataQLTableSchema> GetTableSchemaAsync(
        string sourceKey,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        var (registration, executor) = Resolve(sourceKey);

        await using var session = await registration.SessionFactory(_serviceProvider);
        var schema = await executor.GetTableSchemaAsync(session, tableName, cancellationToken);

        return new DataQLTableSchema
        {
            SourceKey = sourceKey,
            Table = schema.Table,
            Provider = schema.Provider,
            Schema = schema.Schema
        };
    }

    private (DataQLSourceRegistration Registration, IDataQLProviderExecutor Executor) Resolve(string sourceKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            throw new ArgumentException("Source key is required.", nameof(sourceKey));
        }

        if (!_options.Sources.TryGetValue(sourceKey, out var registration))
        {
            throw new InvalidOperationException($"DataQL source '{sourceKey}' is not registered.");
        }

        if (!_executors.TryGetValue(registration.Provider, out var executor))
        {
            throw new NotSupportedException($"No DataQL executor is registered for provider '{registration.Provider}'.");
        }

        return (registration, executor);
    }
}
