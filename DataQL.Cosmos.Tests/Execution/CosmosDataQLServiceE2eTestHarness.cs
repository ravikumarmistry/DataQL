using DataQL;
using DataQL.Cosmos.DependencyInjection;
using DataQL.Cosmos.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DataQL.Cosmos.Tests.Execution;

internal sealed class CosmosDataQLServiceE2eTestHarness : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    private CosmosDataQLServiceE2eTestHarness(
        IDataQLService service,
        IDataQLMetaService metaService,
        ServiceProvider provider)
    {
        Service = service;
        MetaService = metaService;
        _provider = provider;
    }

    public IDataQLService Service { get; }

    public IDataQLMetaService MetaService { get; }

    public static CosmosDataQLServiceE2eTestHarness Create(CosmosE2eFixture fixture)
    {
        if (fixture.Client is null)
        {
            throw new InvalidOperationException("Cosmos e2e fixture was not initialized.");
        }

        var client = fixture.Client;
        var databaseId = fixture.DatabaseId;
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddDataQL(options =>
        {
            options.AddCosmosSource(
                CosmosTestEnvironment.SourceKey,
                _ => client,
                databaseId);
        });

        var provider = services.BuildServiceProvider();
        return new CosmosDataQLServiceE2eTestHarness(
            provider.GetRequiredService<IDataQLService>(),
            provider.GetRequiredService<IDataQLMetaService>(),
            provider);
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
