using DataQL.Ast.Model;
using DataQL.Cosmos.Translation;

namespace DataQL.Cosmos.Tests.Translation;

public class CosmosSqlTranslatorTests
{
    [Fact]
    public void Translate_WithComparisonNode_BuildsParameterizedSql()
    {
        var translator = new CosmosSqlTranslator();
        var filter = new FieldFilter(
            new FieldPath("age"),
            [new ScalarOperation(FieldOperator.Gte, new ScalarValue(18))]);

        var result = translator.Translate(filter);

        Assert.Equal("SELECT * FROM c WHERE c.age >= @p0", result.Sql);
        Assert.True(result.Parameters.ContainsKey("@p0"));
        Assert.Equal(18, result.Parameters["@p0"]);
    }

    [Fact]
    public void Translate_WithLogicalAnd_BuildsExpectedWhereClause()
    {
        var translator = new CosmosSqlTranslator();
        var filter = new LogicalFilter(
            FilterLogicalOperator.And,
            [
                new FieldFilter(new FieldPath("city"), [new ScalarOperation(FieldOperator.Eq, new ScalarValue("Delhi"))]),
                new FieldFilter(new FieldPath("active"), [new ScalarOperation(FieldOperator.Eq, new ScalarValue(true))])
            ]);

        var result = translator.Translate(filter);

        Assert.Equal("SELECT * FROM c WHERE (c.city = @p0) AND (c.active = @p1)", result.Sql);
        Assert.Equal("Delhi", result.Parameters["@p0"]);
        Assert.Equal(true, result.Parameters["@p1"]);
    }

    [Fact]
    public void Translate_WithStringOperators_BuildsFunctionCalls()
    {
        var translator = new CosmosSqlTranslator();
        var filter = new FieldFilter(
            new FieldPath("name"),
            [
                new ScalarOperation(FieldOperator.Contains, new ScalarValue("oh")),
                new ScalarOperation(FieldOperator.StartsWith, new ScalarValue("Jo")),
                new ScalarOperation(FieldOperator.EndsWith, new ScalarValue("hn"))
            ]);

        var result = translator.Translate(filter);

        Assert.Contains("CONTAINS(c.name, @p0)", result.Sql);
        Assert.Contains("STARTSWITH(c.name, @p1)", result.Sql);
        Assert.Contains("ENDSWITH(c.name, @p2)", result.Sql);
    }

    [Fact]
    public void Translate_WithExistsAndIsNull_BuildsExpectedSql()
    {
        var translator = new CosmosSqlTranslator();
        var filter = new FieldFilter(
            new FieldPath("deletedAt"),
            [
                new BooleanOperation(FieldOperator.Exists, true),
                new BooleanOperation(FieldOperator.IsNull, false)
            ]);

        var result = translator.Translate(filter);

        Assert.Contains("IS_DEFINED(c.deletedAt)", result.Sql);
        Assert.Contains("NOT IS_NULL(c.deletedAt)", result.Sql);
    }

    [Fact]
    public void Translate_WithArrayOperators_BuildsExpectedSql()
    {
        var translator = new CosmosSqlTranslator();
        var filter = new FieldFilter(
            new FieldPath("skills"),
            [
                new ListOperation(FieldOperator.ContainsAny, [new ScalarValue("Azure"), new ScalarValue("Go")]),
                new IntegerOperation(FieldOperator.Size, 2),
                new BooleanOperation(FieldOperator.IsEmpty, false)
            ]);

        var result = translator.Translate(filter);

        Assert.Contains("ARRAY_CONTAINS(c.skills, @p0)", result.Sql);
        Assert.Contains("ARRAY_CONTAINS(c.skills, @p1)", result.Sql);
        Assert.Contains("ARRAY_LENGTH(c.skills) = @p2", result.Sql);
        Assert.Contains("ARRAY_LENGTH(c.skills) > 0", result.Sql);
    }

    [Fact]
    public void Translate_WithAny_BuildsExistsSubquery()
    {
        var translator = new CosmosSqlTranslator();
        var filter = new FieldFilter(
            new FieldPath("projects"),
            [
                new AnyOperation(
                    new FieldFilter(
                        new FieldPath("hours"),
                        [new ScalarOperation(FieldOperator.Gt, new ScalarValue(20))]))
            ]);

        var result = translator.Translate(filter);

        Assert.Contains("EXISTS(SELECT VALUE", result.Sql);
        Assert.Contains("FROM", result.Sql);
        Assert.Contains("IN c.projects", result.Sql);
        Assert.Contains("hours >", result.Sql);
    }
}
