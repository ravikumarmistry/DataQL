using DataQL.Contracts;
using DataQL.Validation;

namespace DataQL.Tests.Validation;

public class QueryRequestValidatorTests
{
    [Fact]
    public void Validate_WhenOrderMissing_ReturnsError()
    {
        var validator = new QueryRequestValidator();
        var request = new QueryRequest
        {
            Limit = 10
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "Order.Required");
    }

    [Fact]
    public void Validate_WhenContinuationTokenWithoutLimit_ReturnsError()
    {
        var validator = new QueryRequestValidator();
        var request = new QueryRequest
        {
            ContinuationToken = "abc",
            Order = [new OrderClause { Field = "age", Direction = "asc" }]
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "ContinuationToken.RequiresLimit");
    }

    [Fact]
    public void Validate_WhenLimitIsZero_ReturnsError()
    {
        var validator = new QueryRequestValidator();
        var request = new QueryRequest { Limit = 0 };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "Limit.OutOfRange");
    }

    [Fact]
    public void Validate_WhenOrderDirectionInvalid_ReturnsError()
    {
        var validator = new QueryRequestValidator();
        var request = new QueryRequest
        {
            Order = [new OrderClause { Field = "age", Direction = "up" }]
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "Order.DirectionInvalid");
    }

    [Fact]
    public void Validate_WithValidRequest_ReturnsSuccess()
    {
        var validator = new QueryRequestValidator();
        var request = new QueryRequest
        {
            Limit = 25,
            Order = [new OrderClause { Field = "age", Direction = "desc" }]
        };

        var result = validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenGroupByIsEmpty_ReturnsError()
    {
        var validator = new QueryRequestValidator();
        var request = new QueryRequest
        {
            Group = new GroupRequest
            {
                GroupBy = [],
                Metrics = [new GroupMetricRequest { Field = "*", Operation = "count", Alias = "total" }]
            },
            Order = [new OrderClause { Field = "department", Direction = "asc" }]
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "Group.GroupBy.Required");
    }

    [Fact]
    public void Validate_WhenMetricsIsEmpty_ReturnsError()
    {
        var validator = new QueryRequestValidator();
        var request = new QueryRequest
        {
            Group = new GroupRequest
            {
                GroupBy = ["department"],
                Metrics = []
            },
            Order = [new OrderClause { Field = "department", Direction = "asc" }]
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "Group.Metrics.Required");
    }

    [Fact]
    public void Validate_WhenMetricOperationInvalid_ReturnsError()
    {
        var validator = new QueryRequestValidator();
        var request = new QueryRequest
        {
            Group = new GroupRequest
            {
                GroupBy = ["department"],
                Metrics = [new GroupMetricRequest { Field = "salary", Operation = "median", Alias = "m" }]
            },
            Order = [new OrderClause { Field = "department", Direction = "asc" }]
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "Group.Metric.OperationInvalid");
    }

    [Fact]
    public void Validate_WhenMetricAliasDuplicated_ReturnsError()
    {
        var validator = new QueryRequestValidator();
        var request = new QueryRequest
        {
            Group = new GroupRequest
            {
                GroupBy = ["department"],
                Metrics =
                [
                    new GroupMetricRequest { Field = "*", Operation = "count", Alias = "employees" },
                    new GroupMetricRequest { Field = "salary", Operation = "sum", Alias = "employees" }
                ]
            },
            Order = [new OrderClause { Field = "department", Direction = "asc" }]
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "Group.Metric.AliasDuplicate");
    }

    [Fact]
    public void Validate_WhenWildcardUsedWithNonCount_ReturnsError()
    {
        var validator = new QueryRequestValidator();
        var request = new QueryRequest
        {
            Group = new GroupRequest
            {
                GroupBy = ["department"],
                Metrics = [new GroupMetricRequest { Field = "*", Operation = "sum", Alias = "total" }]
            },
            Order = [new OrderClause { Field = "department", Direction = "asc" }]
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "Group.Metric.FieldWildcardInvalid");
    }

    [Fact]
    public void Validate_WithValidGroupRequest_ReturnsSuccess()
    {
        var validator = new QueryRequestValidator();
        var request = new QueryRequest
        {
            Group = new GroupRequest
            {
                GroupBy = ["department", "city"],
                Metrics =
                [
                    new GroupMetricRequest { Field = "*", Operation = "count", Alias = "employees" },
                    new GroupMetricRequest { Field = "salary", Operation = "avg", Alias = "averageSalary" }
                ]
            },
            Limit = 20,
            Order = [new OrderClause { Field = "employees", Direction = "desc" }]
        };

        var result = validator.Validate(request);

        Assert.True(result.IsValid);
    }
}
