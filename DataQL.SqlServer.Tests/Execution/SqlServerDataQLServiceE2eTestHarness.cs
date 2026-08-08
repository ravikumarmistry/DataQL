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
    private readonly CollectingLoggerProvider _loggerProvider;

    private SqlServerDataQLServiceE2eTestHarness(
        IDataQLService service,
        IDataQLMetaService metaService,
        ServiceProvider provider,
        CollectingLoggerProvider loggerProvider)
    {
        Service = service;
        MetaService = metaService;
        _provider = provider;
        _loggerProvider = loggerProvider;
    }

    public IDataQLService Service { get; }

    public IDataQLMetaService MetaService { get; }

    public IReadOnlyList<string> LogMessages => _loggerProvider.Messages;

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
        var loggerProvider = new CollectingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(loggerProvider);
        });
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
            provider,
            loggerProvider);
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
