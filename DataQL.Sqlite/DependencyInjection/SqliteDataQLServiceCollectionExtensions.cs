using System;
using DataQL.Sqlite.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DataQL.Sqlite.DependencyInjection;

public static class SqliteDataQLServiceCollectionExtensions
{
    public static IServiceCollection AddDataQLSqlite(this IServiceCollection services)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.TryAddSingleton<SqliteQueryExecutionEngine>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDataQLProviderExecutor, SqliteDataQLProviderExecutor>());

        return services;
    }
}
