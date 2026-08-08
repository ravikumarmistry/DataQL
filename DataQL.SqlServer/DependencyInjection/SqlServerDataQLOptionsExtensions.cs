using System;
using System.Data;
using System.Threading.Tasks;
using DataQL.Abstractions;
using DataQL.SqlServer.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DataQL.SqlServer.DependencyInjection;

public static class SqlServerDataQLOptionsExtensions
{
    public static DataQLOptions AddSqlServerSource(
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

        options.Services.TryAddSingleton<ISqlServerQueryExecutor, SqlServerQueryExecutor>();
        options.Services.TryAddSingleton<SqlServerQueryExecutionEngine>(sp =>
            new SqlServerQueryExecutionEngine(
                executor: sp.GetRequiredService<ISqlServerQueryExecutor>()));
        options.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IDataQLProviderExecutor, SqlServerDataQLProviderExecutor>());

        return options.AddSource(
            sourceKey,
            new DataQLSourceRegistration(
                ProviderName.SqlServer,
                sp => CreateSessionAsync(connectionFactory, sp)));
    }

    private static async ValueTask<IDataQLSession> CreateSessionAsync(
        Func<IServiceProvider, IDbConnection> connectionFactory,
        IServiceProvider serviceProvider)
    {
        return await AdoDataQLSession.CreateAsync(
            ProviderName.SqlServer,
            connectionFactory(serviceProvider));
    }
}
