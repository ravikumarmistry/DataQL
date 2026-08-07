using System.Text.Json;
using System.Text.Json.Nodes;
using DataQL.Contracts;

namespace DataQL.Tests.Contracts;

public class QueryFilterBuilderTests
{
    [Fact]
    public void FieldGte_BuildsExpectedFilter()
    {
        var filter = QueryFilterBuilder.Field("Age").Gte(21);

        var node = JsonNode.Parse(filter.ToJsonElement().GetRawText())!.AsObject();
        Assert.Equal(21, node["Age"]!["$gte"]!.GetValue<int>());
    }

    [Fact]
    public void AndOrNot_BuildsExpectedLogicalShape()
    {
        var filter = QueryFilterBuilder.And(
            QueryFilterBuilder.Field("IsActive").Eq(true),
            QueryFilterBuilder.Not(
                QueryFilterBuilder.Or(
                    QueryFilterBuilder.Field("City").Eq("Pune"),
                    QueryFilterBuilder.Field("City").Eq("Mumbai"))));

        var node = JsonNode.Parse(filter.ToJsonElement().GetRawText())!.AsObject();
        var andItems = node["$and"]!.AsArray();

        Assert.Equal(2, andItems.Count);
        Assert.True(andItems[0]!["IsActive"]!.GetValue<bool>());

        var notObject = andItems[1]!["$not"]!.AsObject();
        var orItems = notObject["$or"]!.AsArray();
        Assert.Equal("Pune", orItems[0]!["City"]!.GetValue<string>());
        Assert.Equal("Mumbai", orItems[1]!["City"]!.GetValue<string>());
    }

    [Fact]
    public void MergeAnd_WithExistingWhere_MergesWithGuard()
    {
        var existing = JsonDocument.Parse("{\"City\":\"Delhi\"}").RootElement;
        var guard = QueryFilterBuilder.Field("IsActive").Eq(true);

        var merged = QueryFilterBuilder.MergeAnd(existing, guard);
        var node = JsonNode.Parse(merged.GetRawText())!.AsObject();
        var andItems = node["$and"]!.AsArray();

        Assert.Equal(2, andItems.Count);
        Assert.Equal("Delhi", andItems[0]!["City"]!.GetValue<string>());
        Assert.True(andItems[1]!["IsActive"]!.GetValue<bool>());
    }

    [Fact]
    public void MergeAnd_WithoutExistingWhere_ReturnsGuard()
    {
        var guard = QueryFilterBuilder.Field("IsActive").Eq(true);

        var merged = QueryFilterBuilder.MergeAnd(null, guard);
        var node = JsonNode.Parse(merged.GetRawText())!.AsObject();

        Assert.True(node["IsActive"]!.GetValue<bool>());
    }
}
