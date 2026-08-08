using DataQL.SqlServer.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace DataQL.SqlServer.Tests.DependencyInjection;

public class SqlServerDataQLOptionsExtensionsTests
{
    [Fact]
    public void AddSqlServerSource_WithNullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            SqlServerDataQLOptionsExtensions.AddSqlServerSource(null!, "sample", _ => null!));
    }

    [Fact]
    public void AddSqlServerSource_WithEmptySourceKey_ThrowsArgumentException()
    {
        var options = new DataQLOptions(new ServiceCollection());

        var ex = Assert.Throws<ArgumentException>(() =>
            options.AddSqlServerSource("  ", _ => null!));

        Assert.Contains("Source key", ex.Message);
    }

    [Fact]
    public void AddSqlServerSource_WithNullConnectionFactory_ThrowsArgumentNullException()
    {
        var options = new DataQLOptions(new ServiceCollection());

        Assert.Throws<ArgumentNullException>(() =>
            options.AddSqlServerSource("sample", null!));
    }
}
