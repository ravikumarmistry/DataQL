using System.Collections.Generic;
using System.Text.Json;
using DataQL.Abstractions;
using DataQL.Ast.Parsing;
using DataQL.Contracts;
using DataQL.Pipeline;
using DataQL.Validation;

namespace DataQL.Tests.Validation;

public class ProviderCapabilityValidatorTests
{
    private readonly QueryProcessor _processor = new(
        new QueryRequestValidator(),
        new QueryAstParser(),
        new AstSemanticValidator());

    private readonly ProviderCapabilityValidator _validator = new();

    private static ProviderCapabilities SqliteLikeCapabilities() => new()
    {
        Provider = "sqlite",
        SupportedOperators = new HashSet<string>
        {
            "$eq", "$ne", "$gt", "$gte", "$lt", "$lte",
            "$in", "$nin",
            "$contains", "$startsWith", "$endsWith",
            "$exists", "$isNull",
            "$and", "$or", "$not"
        },
        SupportsSelect = true,
        SupportsExclude = true,
        SupportsGrouping = true,
        SupportsHaving = true,
        SupportsNestedFields = false,
        SupportedGroupOperations = new HashSet<string> { "count", "sum", "avg", "min", "max" }
    };

    private static JsonElement JsonFilter(string json)
        => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void Validate_UnsupportedOperator_ReturnsCapabilityError()
    {
        var request = new QueryRequest
        {
            Order = [new OrderClause { Field = "Id", Direction = "asc" }],
            Where = JsonFilter("""{"Name":{"$regex":"Ada.*"}}""")
        };

        var ast = _processor.Process(request);
        var result = _validator.Validate(ast, SqliteLikeCapabilities());

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Capability.OperatorNotSupported", error.Code);
        Assert.Equal("sqlite", error.Provider);
        Assert.Contains("$regex", error.Message);
        Assert.NotNull(error.Details);
        Assert.Equal("$regex", error.Details!["operator"]);
    }

    [Fact]
    public void Validate_UnsupportedGroupOperation_ReturnsCapabilityError()
    {
        var request = new QueryRequest
        {
            Order = [new OrderClause { Field = "Department", Direction = "asc" }],
            Group = new GroupRequest
            {
                GroupBy = ["Department"],
                Metrics =
                [
                    new GroupMetricRequest
                    {
                        Field = "Name",
                        Operation = "first",
                        Alias = "firstName"
                    }
                ]
            }
        };

        var ast = _processor.Process(request);
        var result = _validator.Validate(ast, SqliteLikeCapabilities());

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Capability.GroupOperationNotSupported", error.Code);
        Assert.Equal("first", error.Details!["operation"]);
    }

    [Fact]
    public void Validate_HavingWhenNotSupported_ReturnsCapabilityError()
    {
        var capabilities = new ProviderCapabilities
        {
            Provider = "cosmos",
            SupportedOperators = SqliteLikeCapabilities().SupportedOperators,
            SupportsSelect = true,
            SupportsExclude = true,
            SupportsGrouping = true,
            SupportsHaving = false,
            SupportsNestedFields = true,
            SupportedGroupOperations = SqliteLikeCapabilities().SupportedGroupOperations
        };

        var request = new QueryRequest
        {
            Order = [new OrderClause { Field = "Department", Direction = "asc" }],
            Group = new GroupRequest
            {
                GroupBy = ["Department"],
                Metrics =
                [
                    new GroupMetricRequest
                    {
                        Field = "*",
                        Operation = "count",
                        Alias = "employees"
                    }
                ],
                Having = QueryFilterBuilder.Field("employees").Gt(1).ToJsonElement()
            }
        };

        var ast = _processor.Process(request);
        var result = _validator.Validate(ast, capabilities);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "Capability.HavingNotSupported");
    }

    [Fact]
    public void Validate_NestedFieldWhenNotSupported_ReturnsCapabilityError()
    {
        var request = new QueryRequest
        {
            Order = [new OrderClause { Field = "Id", Direction = "asc" }],
            Where = QueryFilterBuilder.Field("Address.City").Eq("Seattle").ToJsonElement()
        };

        var ast = _processor.Process(request);
        var result = _validator.Validate(ast, SqliteLikeCapabilities());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "Capability.NestedFieldsNotSupported");
    }

    [Fact]
    public void EnsureValid_ThrowsAstValidationException()
    {
        var request = new QueryRequest
        {
            Order = [new OrderClause { Field = "Id", Direction = "asc" }],
            Where = JsonFilter("""{"Name":{"$regex":"Ada.*"}}""")
        };

        var ast = _processor.Process(request);
        var ex = Assert.Throws<AstValidationException>(() =>
            _validator.EnsureValid(ast, SqliteLikeCapabilities()));

        Assert.Equal("Capability.OperatorNotSupported", Assert.Single(ex.Errors).Code);
    }

    [Fact]
    public void Validate_SupportedQuery_Succeeds()
    {
        var request = new QueryRequest
        {
            Order = [new OrderClause { Field = "Id", Direction = "asc" }],
            Where = QueryFilterBuilder.Field("Age").Gte(30).ToJsonElement(),
            Group = new GroupRequest
            {
                GroupBy = ["Department"],
                Metrics =
                [
                    new GroupMetricRequest
                    {
                        Field = "*",
                        Operation = "count",
                        Alias = "employees"
                    }
                ],
                Having = QueryFilterBuilder.Field("employees").Gt(0).ToJsonElement()
            }
        };

        var ast = _processor.Process(request);
        var result = _validator.Validate(ast, SqliteLikeCapabilities());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_DistinctWhenNotSupported_ReturnsCapabilityError()
    {
        var request = new QueryRequest
        {
            Distinct = ["City"],
            Order = [new OrderClause { Field = "City", Direction = "asc" }]
        };

        var ast = _processor.Process(request);
        var result = _validator.Validate(ast, SqliteLikeCapabilities());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "Capability.DistinctNotSupported");
    }

    [Fact]
    public void Validate_DistinctWhenSupported_Succeeds()
    {
        var capabilities = new ProviderCapabilities
        {
            Provider = "sqlite",
            SupportedOperators = SqliteLikeCapabilities().SupportedOperators,
            SupportsSelect = true,
            SupportsExclude = true,
            SupportsGrouping = true,
            SupportsHaving = true,
            SupportsNestedFields = false,
            SupportsDistinct = true,
            SupportedGroupOperations = SqliteLikeCapabilities().SupportedGroupOperations
        };

        var request = new QueryRequest
        {
            Distinct = ["City", "Department"],
            Select = ["City"],
            Order = [new OrderClause { Field = "City", Direction = "asc" }]
        };

        var ast = _processor.Process(request);
        var result = _validator.Validate(ast, capabilities);

        Assert.True(result.IsValid);
    }
}
