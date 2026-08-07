using System;
using System.Data;
using DataQL.Abstractions;
using DataQL.Sqlite.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DataQL.Sqlite.DependencyInjection;

public static class SqliteDataQLOptionsExtensions
{
    public static DataQLOptions AddSqliteSource(
        this DataQLOptions options,
        string sourceKey,
        Func<IServiceProvider, IDbConnection> connectionFactory)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            throw new ArgumentException("Source key is required.", nameof(sourceKey));
        }

        if (connectionFactory is null)
        {
            throw new ArgumentNullException(nameof(connectionFactory));
        }

        options.Services.TryAddSingleton<SqliteQueryExecutionEngine>();
        options.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IDataQLProviderExecutor, SqliteDataQLProviderExecutor>());

        return options.AddSource(
            sourceKey,
            new DataQLSourceRegistration(
                ProviderName.Sqlite,
                connectionFactory));
    }
}
