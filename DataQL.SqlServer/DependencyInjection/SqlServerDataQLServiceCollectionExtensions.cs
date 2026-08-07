using System;
using DataQL.SqlServer.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DataQL.SqlServer.DependencyInjection;

public static class SqlServerDataQLServiceCollectionExtensions
{
    public static IServiceCollection AddDataQLSqlServer(this IServiceCollection services)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.TryAddSingleton<SqlServerQueryExecutionEngine>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDataQLProviderExecutor, SqlServerDataQLProviderExecutor>());

        return services;
    }
}
