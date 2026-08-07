using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DataQL.AspNetCore.OpenApi;

public interface IDataQLOpenApiDocumentProvider
{
    Task<JsonElement> GetDocumentAsync(
        string prefix,
        bool refresh = false,
        CancellationToken cancellationToken = default);
}

public sealed class DataQLOpenApiDocumentProvider : IDataQLOpenApiDocumentProvider
{
    private readonly DataQLOpenApiDocumentBuilder _builder;
    private readonly ConcurrentDictionary<string, JsonElement> _cache = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public DataQLOpenApiDocumentProvider(DataQLOpenApiDocumentBuilder builder)
    {
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
    }

    public async Task<JsonElement> GetDocumentAsync(
        string prefix,
        bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("Prefix is required.", nameof(prefix));
        }

        var cacheKey = NormalizePrefix(prefix);

        if (!refresh && _cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!refresh && _cache.TryGetValue(cacheKey, out cached))
            {
                return cached;
            }

            var document = await _builder.BuildAsync(cacheKey, cancellationToken);
            var element = JsonSerializer.SerializeToElement(document);
            _cache[cacheKey] = element;
            return element;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string NormalizePrefix(string prefix)
    {
        var normalized = prefix.StartsWith('/')
            ? prefix
            : "/" + prefix;
        return normalized.TrimEnd('/');
    }
}
