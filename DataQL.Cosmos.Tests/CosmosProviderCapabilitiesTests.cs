namespace DataQL.Cosmos.Tests;

public class CosmosProviderCapabilitiesTests
{
    [Fact]
    public void Capabilities_AdvertisesGroupingAndSupportedGroupOperations()
    {
        var translator = new DataQL.Cosmos.CosmosQueryTranslator();

        Assert.True(translator.Capabilities.SupportsSelect);
        Assert.False(translator.Capabilities.SupportsExclude);
        Assert.False(translator.Capabilities.SupportsDistinct);
        Assert.True(translator.Capabilities.SupportsGrouping);
        Assert.False(translator.Capabilities.SupportsHaving);
        Assert.True(translator.Capabilities.SupportsNestedFields);
        Assert.Contains("count", translator.Capabilities.SupportedGroupOperations);
        Assert.Contains("sum", translator.Capabilities.SupportedGroupOperations);
        Assert.Contains("avg", translator.Capabilities.SupportedGroupOperations);
        Assert.Contains("min", translator.Capabilities.SupportedGroupOperations);
        Assert.Contains("max", translator.Capabilities.SupportedGroupOperations);
        Assert.DoesNotContain("first", translator.Capabilities.SupportedGroupOperations);
        Assert.DoesNotContain("last", translator.Capabilities.SupportedGroupOperations);
    }
}
