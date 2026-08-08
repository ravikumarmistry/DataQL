using DataQL.Abstractions;
using DataQL.Ast.Model;
using DataQL.Contracts;
using DataQL.Cosmos.Validation;
using DataQL.Validation;

namespace DataQL.Cosmos.Tests.Validation;

public class CosmosProviderQueryValidatorTests
{
    private readonly CosmosProviderQueryValidator _validator = CosmosProviderQueryValidator.Instance;
    private readonly ProviderCapabilities _capabilities = new CosmosQueryTranslator().Capabilities;

    [Fact]
    public void Validate_WithEmptyOrder_ReturnsSuccess()
    {
        var request = new QueryRequest { Limit = 2 };
        var ast = EmptyAst();

        var result = _validator.Validate(ast, request, _capabilities);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithGroupAndContinuation_ReturnsError()
    {
        var request = new QueryRequest
        {
            ContinuationToken = "token",
            Limit = 2,
            Order = [new OrderClause { Field = "Department", Direction = "asc" }],
            Group = new GroupRequest
            {
                GroupBy = ["Department"],
                Metrics = [new GroupMetricRequest { Field = "*", Operation = "count", Alias = "Employees" }]
            }
        };
        var ast = GroupAst();

        var result = _validator.Validate(ast, request, _capabilities);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "Provider.Group.ContinuationNotSupported");
    }

    [Fact]
    public void Validate_WithGroupAndIncludeCount_ReturnsError()
    {
        var request = new QueryRequest
        {
            IncludeCount = true,
            Group = new GroupRequest
            {
                GroupBy = ["Department"],
                Metrics = [new GroupMetricRequest { Field = "*", Operation = "count", Alias = "Employees" }]
            }
        };
        var ast = GroupAst();

        var result = _validator.Validate(ast, request, _capabilities);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "Provider.Group.IncludeCountNotSupported");
    }

    [Fact]
    public void Validate_WithGroupAndLimit_ReturnsSuccess()
    {
        var request = new QueryRequest
        {
            Limit = 1,
            Group = new GroupRequest
            {
                GroupBy = ["Department"],
                Metrics = [new GroupMetricRequest { Field = "*", Operation = "count", Alias = "Employees" }]
            }
        };
        var ast = GroupAst();

        var result = _validator.Validate(ast, request, _capabilities);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Capabilities_IncludeDescriptionAndNotes()
    {
        Assert.False(string.IsNullOrWhiteSpace(_capabilities.Description));
        Assert.Contains(_capabilities.Notes, n => n.Code == "Group.NoContinuation");
        Assert.Contains(_capabilities.Notes, n => n.Code == "Count.ExtraQuery");
    }

    private static QueryAst EmptyAst() =>
        new(
            null,
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(2, null, false, false),
            null);

    private static QueryAst GroupAst() =>
        new(
            null,
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(null, null, false, false),
            new GroupAst(
                [new FieldPath("Department")],
                [new GroupMetricAst(new FieldPath("*"), GroupMetricOperation.Count, "Employees")]));
}
