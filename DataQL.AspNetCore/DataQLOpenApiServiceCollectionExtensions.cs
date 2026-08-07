using DataQL.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DataQL.AspNetCore;

public static class DataQLOpenApiServiceCollectionExtensions
{
    public static IServiceCollection AddDataQLOpenApi(this IServiceCollection services)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.TryAddSingleton<DataQLOpenApiDocumentBuilder>();
        services.TryAddSingleton<IDataQLOpenApiDocumentProvider, DataQLOpenApiDocumentProvider>();
        return services;
    }
}
