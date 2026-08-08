using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DataQL.Abstractions;
using DataQL.Contracts;

namespace DataQL;

public sealed class DataQLService : IDataQLService
{
    private readonly DataQLOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadOnlyDictionary<string, IDataQLProviderExecutor> _executors;

    public DataQLService(
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

    public async Task<QueryResponse<T>> ExecuteAsync<T>(
        string sourceKey,
        string sourceName,
        QueryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(sourceName))
        {
            throw new ArgumentException("Source name is required.", nameof(sourceName));
        }

        var sourceRegistration = GetRegistration(sourceKey);
        var source = new QuerySource(
            sourceRegistration.Provider,
            Name: sourceName);

        if (!_executors.TryGetValue(sourceRegistration.Provider, out var executor))
        {
            throw new NotSupportedException($"No DataQL executor is registered for provider '{sourceRegistration.Provider}'.");
        }

        await using var session = await sourceRegistration.SessionFactory(_serviceProvider);

        var stopwatch = Stopwatch.StartNew();
        var response = await executor.ExecuteAsync<T>(
            session,
            source,
            request,
            cancellationToken);
        stopwatch.Stop();

        return WithMeta(response, sourceRegistration.Provider, stopwatch.ElapsedMilliseconds);
    }

    private static QueryResponse<T> WithMeta<T>(
        QueryResponse<T> response,
        string provider,
        long executionTimeMs)
    {
        return new QueryResponse<T>
        {
            Results = response.Results,
            HasMore = response.HasMore,
            ContinuationToken = response.ContinuationToken,
            Count = response.Count,
            Meta = new QueryExecutionMeta
            {
                Provider = provider,
                ExecutionTimeMs = executionTimeMs,
                Extensions = response.Meta?.Extensions
            }
        };
    }

    private DataQLSourceRegistration GetRegistration(string sourceKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            throw new ArgumentException("Source key is required.", nameof(sourceKey));
        }

        if (!_options.Sources.TryGetValue(sourceKey, out var sourceRegistration))
        {
            throw new InvalidOperationException($"DataQL source '{sourceKey}' is not registered.");
        }

        return sourceRegistration;
    }
}
