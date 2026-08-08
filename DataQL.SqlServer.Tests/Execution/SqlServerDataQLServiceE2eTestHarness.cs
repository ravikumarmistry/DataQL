using DataQL;
using DataQL.SqlServer.DependencyInjection;
using DataQL.SqlServer.Tests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DataQL.SqlServer.Tests.Execution;

internal sealed class SqlServerDataQLServiceE2eTestHarness : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    private SqlServerDataQLServiceE2eTestHarness(
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

    public static SqlServerDataQLServiceE2eTestHarness Create(
        SqlServerE2eFixture fixture,
        Action<DataQLOptions>? configure = null)
    {
        if (string.IsNullOrWhiteSpace(fixture.ConnectionString))
        {
            throw new InvalidOperationException(
                "SQL Server e2e fixture was not initialized. Is SQL Server available?");
        }

        var connectionString = fixture.ConnectionString;
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddDataQL(options =>
        {
            options.AddSqlServerSource(
                SqlServerTestEnvironment.SourceKey,
                _ => new SqlConnection(connectionString));
            configure?.Invoke(options);
        });

        var provider = services.BuildServiceProvider();
        return new SqlServerDataQLServiceE2eTestHarness(
            provider.GetRequiredService<IDataQLService>(),
            provider.GetRequiredService<IDataQLMetaService>(),
            provider);
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
