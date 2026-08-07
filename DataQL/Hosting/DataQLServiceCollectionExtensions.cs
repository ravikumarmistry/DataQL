using System;
using DataQL.Ast.Parsing;
using DataQL.Pipeline;
using DataQL.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace DataQL;

public static class DataQLServiceCollectionExtensions
{
    public static IServiceCollection AddDataQL(
        this IServiceCollection services,
        Action<DataQLOptions> configure)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var options = new DataQLOptions(services);
        configure(options);

        services.AddSingleton<QueryProcessor>(_ => new QueryProcessor(
            new QueryRequestValidator(),
            new QueryAstParser(),
            new AstSemanticValidator()));

        services.AddSingleton(options);
        services.AddSingleton<IDataQLService, DataQLService>();
        services.AddSingleton<IDataQLMetaService, DataQLMetaService>();

        return services;
    }
}
