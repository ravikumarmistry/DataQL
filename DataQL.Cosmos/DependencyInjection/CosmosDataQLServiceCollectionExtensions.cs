using DataQL.Cosmos.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DataQL.Cosmos.DependencyInjection;

public static class CosmosDataQLServiceCollectionExtensions
{
    public static IServiceCollection AddDataQLCosmos(this IServiceCollection services)
    {
        services.TryAddSingleton<CosmosQueryExecutionEngine>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDataQLProviderExecutor, CosmosDataQLProviderExecutor>());
        return services;
    }
}
