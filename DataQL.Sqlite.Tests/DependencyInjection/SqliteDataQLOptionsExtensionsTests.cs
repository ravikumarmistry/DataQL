using DataQL.Sqlite.DependencyInjection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

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
}
