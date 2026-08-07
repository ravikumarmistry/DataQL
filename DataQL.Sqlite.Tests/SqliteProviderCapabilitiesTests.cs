namespace DataQL.Sqlite.Tests;

public class SqliteProviderCapabilitiesTests
{
    [Fact]
    public void Capabilities_AdvertisesGroupingAndSupportedGroupOperations()
    {
        var translator = new DataQL.Sqlite.SqliteQueryTranslator();

        Assert.True(translator.Capabilities.SupportsSelect);
        Assert.True(translator.Capabilities.SupportsExclude);
        Assert.True(translator.Capabilities.SupportsGrouping);
        Assert.True(translator.Capabilities.SupportsHaving);
        Assert.False(translator.Capabilities.SupportsNestedFields);
        Assert.True(translator.Capabilities.SupportsDistinct);
        Assert.Contains("count", translator.Capabilities.SupportedGroupOperations);
        Assert.Contains("sum", translator.Capabilities.SupportedGroupOperations);
        Assert.Contains("avg", translator.Capabilities.SupportedGroupOperations);
        Assert.Contains("min", translator.Capabilities.SupportedGroupOperations);
        Assert.Contains("max", translator.Capabilities.SupportedGroupOperations);
        Assert.DoesNotContain("first", translator.Capabilities.SupportedGroupOperations);
        Assert.DoesNotContain("last", translator.Capabilities.SupportedGroupOperations);
    }
}
