namespace DataQL.SqlServer.Tests;

public class SqlServerProviderCapabilitiesTests
{
    [Fact]
    public void Capabilities_AdvertisesGroupingAndSupportedGroupOperations()
    {
        var translator = new DataQL.SqlServer.SqlServerQueryTranslator();

        Assert.True(translator.Capabilities.SupportsSelect);
        Assert.True(translator.Capabilities.SupportsExclude);
        Assert.True(translator.Capabilities.SupportsGrouping);
        Assert.True(translator.Capabilities.SupportsHaving);
        Assert.True(translator.Capabilities.SupportsNestedFields);
        Assert.True(translator.Capabilities.SupportsDistinct);
        Assert.Contains("$any", translator.Capabilities.SupportedOperators);
        Assert.Contains("$size", translator.Capabilities.SupportedOperators);
        Assert.DoesNotContain("$regex", translator.Capabilities.SupportedOperators);
        Assert.Contains("count", translator.Capabilities.SupportedGroupOperations);
        Assert.Contains("sum", translator.Capabilities.SupportedGroupOperations);
        Assert.Contains("avg", translator.Capabilities.SupportedGroupOperations);
        Assert.Contains("min", translator.Capabilities.SupportedGroupOperations);
        Assert.Contains("max", translator.Capabilities.SupportedGroupOperations);
        Assert.DoesNotContain("first", translator.Capabilities.SupportedGroupOperations);
        Assert.DoesNotContain("last", translator.Capabilities.SupportedGroupOperations);
    }
}
