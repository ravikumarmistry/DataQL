using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace DataQL;

public sealed class DataQLOptions
{
    private readonly Dictionary<string, DataQLSourceRegistration> _sources =
        new(StringComparer.OrdinalIgnoreCase);

    public IServiceCollection Services { get; }

    public DataQLOptions(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public IReadOnlyDictionary<string, DataQLSourceRegistration> Sources => _sources;

    public DataQLOptions AddSource(string key, DataQLSourceRegistration registration)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Source key is required.", nameof(key));
        }

        if (registration is null)
        {
            throw new ArgumentNullException(nameof(registration));
        }

        _sources[key] = registration;
        return this;
    }
}
