using DataQL.Abstractions;
using DataQL.Ast.Model;
using DataQL.Contracts;
using DataQL.SqlServer.Validation;
using DataQL.Validation;

namespace DataQL.SqlServer.Tests.Validation;

public class SqlServerProviderQueryValidatorTests
{
    private readonly SqlServerProviderQueryValidator _validator = SqlServerProviderQueryValidator.Instance;
    private readonly ProviderCapabilities _capabilities = new SqlServerQueryTranslator().Capabilities;

    [Fact]
    public void Validate_WhenOrderMissing_ReturnsOrderRequired()
    {
        var result = _validator.Validate(EmptyAst(), new QueryRequest { Limit = 10 }, _capabilities);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "Order.Required");
    }

    [Fact]
    public void Validate_WhenOrderPresent_ReturnsSuccess()
    {
        var request = new QueryRequest
        {
            Limit = 10,
            Order = [new OrderClause { Field = "Age", Direction = "asc" }]
        };

        var result = _validator.Validate(EmptyAst(), request, _capabilities);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Capabilities_IncludeDescriptionAndNotes()
    {
        Assert.False(string.IsNullOrWhiteSpace(_capabilities.Description));
        Assert.Contains(_capabilities.Notes, n => n.Code == "Order.Required");
    }

    private static QueryAst EmptyAst() =>
        new(
            null,
            new ProjectionAst([], [], []),
            [],
            new PaginationAst(10, null, false, false),
            null);
}
