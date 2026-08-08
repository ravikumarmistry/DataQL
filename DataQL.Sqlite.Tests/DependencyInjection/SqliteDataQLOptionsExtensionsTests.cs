using DataQL;
using DataQL.Sqlite.DependencyInjection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DataQL.Sqlite.Tests.DependencyInjection;

public class SqliteDataQLOptionsExtensionsTests
{
    [Fact]
    public void AddSqliteSource_WithNullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            SqliteDataQLOptionsExtensions.AddSqliteSource(null!, "sample", _ => new SqliteConnection("Data Source=:memory:")));
    }

    [Fact]
    public void AddSqliteSource_WithEmptySourceKey_ThrowsArgumentException()
    {
        var options = new DataQLOptions(new ServiceCollection());

        var ex = Assert.Throws<ArgumentException>(() =>
            options.AddSqliteSource("  ", _ => new SqliteConnection("Data Source=:memory:")));

        Assert.Contains("Source key", ex.Message);
    }

    [Fact]
    public void AddSqliteSource_WithNullConnectionFactory_ThrowsArgumentNullException()
    {
        var options = new DataQLOptions(new ServiceCollection());

        Assert.Throws<ArgumentNullException>(() =>
            options.AddSqliteSource("sample", null!));
    }

    [Fact]
    public void AddSqliteSource_RegistersQueryExecutorWithLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataQL(options =>
            options.AddSqliteSource("sample", _ => new SqliteConnection("Data Source=:memory:")));

        using var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<DataQL.Sqlite.Execution.ISqliteQueryExecutor>();
        var engine = provider.GetRequiredService<DataQL.Sqlite.Execution.SqliteQueryExecutionEngine>();

        Assert.NotNull(executor);
        Assert.NotNull(engine);
        Assert.IsType<DataQL.Sqlite.Execution.SqliteQueryExecutor>(executor);
    }
}
