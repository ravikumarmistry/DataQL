using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using DataQL.Abstractions;
using DataQL.Contracts;
using DataQL.SqlServer.Execution;
using DataQL.SqlServer.Translation;
using DataQL.Token;

namespace DataQL.SqlServer.Tests.Execution;

public class SqlServerQueryExecutionEngineTests
{
    [Fact]
    public async Task ExecuteAsync_WithLimitAndIncludeCount_ReturnsTrimmedPageAndCount()
    {
        var fakeExecutor = new FakeExecutor(
            rows: [
                new PersonRow { Name = "Riya" },
                new PersonRow { Name = "Arun" },
                new PersonRow { Name = "Karan" }
            ],
            count: 3);

        var engine = new SqlServerQueryExecutionEngine(executor: fakeExecutor);
        var source = new QuerySource(ProviderName.SqlServer, "Employees");

        using var connection = new FakeConnection();
        var request = new QueryRequest
        {
            Where = JsonDocument.Parse("{\"Age\":{\"$gte\":21}}").RootElement,
            Order = [new OrderClause { Field = "Age", Direction = "desc" }],
            Limit = 2,
            IncludeCount = true
        };

        var response = await engine.ExecuteAsync<PersonRow>(connection, source, request);

        Assert.Equal(2, response.Results.Count);
        Assert.Equal("Riya", response.Results[0].Name);
        Assert.Equal("Arun", response.Results[1].Name);
        Assert.True(response.HasMore);
        Assert.Equal(3, response.Count);

        Assert.NotNull(fakeExecutor.LastRowsSql);
        Assert.Contains("TOP (3)", fakeExecutor.LastRowsSql);
        Assert.NotNull(fakeExecutor.LastCountSql);
        Assert.Contains("FROM [Employees] AS [t]", fakeExecutor.LastCountSql);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutLimit_ReturnsAllRowsAndNoHasMore()
    {
        var fakeExecutor = new FakeExecutor(
            rows: [
                new PersonRow { Name = "Asha" },
                new PersonRow { Name = "Riya" }
            ],
            count: 2);

        var engine = new SqlServerQueryExecutionEngine(executor: fakeExecutor);
        var source = new QuerySource(ProviderName.SqlServer, "Employees");

        using var connection = new FakeConnection();
        var request = new QueryRequest
        {
            Where = JsonDocument.Parse("{\"City\":\"Delhi\"}").RootElement,
            Order = [new OrderClause { Field = "Name", Direction = "asc" }],
            IncludeCount = false
        };

        var response = await engine.ExecuteAsync<PersonRow>(connection, source, request);

        Assert.Equal(2, response.Results.Count);
        Assert.False(response.HasMore);
        Assert.Null(response.Count);
        Assert.NotNull(fakeExecutor.LastRowsSql);
        Assert.DoesNotContain("TOP (", fakeExecutor.LastRowsSql);
        Assert.Null(fakeExecutor.LastCountSql);
    }

    [Fact]
    public async Task ExecuteAsync_WithContinuationToken_ReturnsNextPage()
    {
        var fakeExecutor = new FakePagedExecutor(
            firstRows: [
                new PersonRow { Name = "Riya", Age = 31 },
                new PersonRow { Name = "Arun", Age = 24 },
                new PersonRow { Name = "Karan", Age = 22 }
            ],
            secondRows: [
                new PersonRow { Name = "Karan", Age = 22 },
                new PersonRow { Name = "Asha", Age = 19 }
            ],
            count: 4);

        var engine = new SqlServerQueryExecutionEngine(executor: fakeExecutor);
        var source = new QuerySource(ProviderName.SqlServer, "Employees");

        using var connection = new FakeConnection();
        var firstRequest = new QueryRequest
        {
            Order = [new OrderClause { Field = "Age", Direction = "desc" }],
            Limit = 2,
            IncludeCount = false
        };

        var firstResponse = await engine.ExecuteAsync<PersonRow>(connection, source, firstRequest);
        Assert.True(firstResponse.HasMore);
        Assert.NotNull(firstResponse.ContinuationToken);
        Assert.Equal(2, firstResponse.Results.Count);
        Assert.Equal("Riya", firstResponse.Results[0].Name);
        Assert.Equal("Arun", firstResponse.Results[1].Name);

        var secondRequest = new QueryRequest
        {
            Order = [new OrderClause { Field = "Age", Direction = "desc" }],
            Limit = 2,
            ContinuationToken = firstResponse.ContinuationToken,
            IncludeCount = false
        };

        var secondResponse = await engine.ExecuteAsync<PersonRow>(connection, source, secondRequest);
        Assert.False(secondResponse.HasMore);
        Assert.Null(secondResponse.ContinuationToken);
        Assert.Equal(2, secondResponse.Results.Count);
        Assert.Equal("Karan", secondResponse.Results[0].Name);
        Assert.Equal("Asha", secondResponse.Results[1].Name);
        Assert.NotNull(fakeExecutor.LastRowsSql);
        Assert.Contains("[t].[Age] <", fakeExecutor.LastRowsSql);
    }

    [Fact]
    public async Task ExecuteAsync_WithContinuationTokenAndChangedRequestShape_ThrowsArgumentException()
    {
        var fakeExecutor = new FakePagedExecutor(
            firstRows: [
                new PersonRow { Name = "Riya", Age = 31 },
                new PersonRow { Name = "Arun", Age = 24 },
                new PersonRow { Name = "Karan", Age = 22 }
            ],
            secondRows: [
                new PersonRow { Name = "Karan", Age = 22 },
                new PersonRow { Name = "Asha", Age = 19 }
            ],
            count: 4);

        var engine = new SqlServerQueryExecutionEngine(executor: fakeExecutor);
        var source = new QuerySource(ProviderName.SqlServer, "Employees");

        using var connection = new FakeConnection();
        var firstRequest = new QueryRequest
        {
            Order = [new OrderClause { Field = "Age", Direction = "desc" }],
            Limit = 2,
            IncludeCount = false
        };

        var firstResponse = await engine.ExecuteAsync<PersonRow>(connection, source, firstRequest);
        Assert.NotNull(firstResponse.ContinuationToken);

        var changedRequest = new QueryRequest
        {
            Where = JsonDocument.Parse("{\"Department\":\"Engineering\"}").RootElement,
            Order = [new OrderClause { Field = "Age", Direction = "desc" }],
            Limit = 2,
            ContinuationToken = firstResponse.ContinuationToken,
            IncludeCount = false
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            engine.ExecuteAsync<PersonRow>(connection, source, changedRequest));
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequestInvalid_ThrowsAstValidationException()
    {
        var fakeExecutor = new FakeExecutor(rows: [], count: 0);
        var engine = new SqlServerQueryExecutionEngine(executor: fakeExecutor);
        var source = new QuerySource(ProviderName.SqlServer, "Employees");

        using var connection = new FakeConnection();
        var request = new QueryRequest { Limit = 0 };

        await Assert.ThrowsAsync<DataQL.Validation.AstValidationException>(() =>
            engine.ExecuteAsync<PersonRow>(connection, source, request));

        Assert.Null(fakeExecutor.LastRowsSql);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidContinuationToken_ThrowsArgumentException()
    {
        var fakeExecutor = new FakeExecutor(rows: [], count: 0);
        var engine = new SqlServerQueryExecutionEngine(executor: fakeExecutor);
        var source = new QuerySource(ProviderName.SqlServer, "Employees");

        using var connection = new FakeConnection();
        var request = new QueryRequest
        {
            Order = [new OrderClause { Field = "Age", Direction = "desc" }],
            Limit = 2,
            ContinuationToken = "not-a-valid-token"
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            engine.ExecuteAsync<PersonRow>(connection, source, request));
        Assert.Contains("Invalid continuation token", ex.Message);
        Assert.Null(fakeExecutor.LastRowsSql);
    }

    [Fact]
    public async Task ExecuteAsync_WithProviderMismatchContinuationToken_ThrowsArgumentException()
    {
        var protector = new Base64ContinuationTokenProtector();
        var token = protector.Protect(new ContinuationTokenEnvelope
        {
            Provider = ProviderName.Sqlite,
            QueryShapeHash = "abc",
            ProviderToken = "{}"
        });

        var fakeExecutor = new FakeExecutor(rows: [], count: 0);
        var engine = new SqlServerQueryExecutionEngine(executor: fakeExecutor, tokenProtector: protector);
        var source = new QuerySource(ProviderName.SqlServer, "Employees");

        using var connection = new FakeConnection();
        var request = new QueryRequest
        {
            Order = [new OrderClause { Field = "Age", Direction = "desc" }],
            Limit = 2,
            ContinuationToken = token
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            engine.ExecuteAsync<PersonRow>(connection, source, request));
        Assert.Contains("provider mismatch", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullConnection_ThrowsArgumentNullException()
    {
        var engine = new SqlServerQueryExecutionEngine(executor: new FakeExecutor([], 0));
        var source = new QuerySource(ProviderName.SqlServer, "Employees");

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            engine.ExecuteAsync<PersonRow>(null!, source, new QueryRequest()));
    }

    [Fact]
    public async Task ExecuteAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        var engine = new SqlServerQueryExecutionEngine(executor: new FakeExecutor([], 0));
        var source = new QuerySource(ProviderName.SqlServer, "Employees");

        using var connection = new FakeConnection();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            engine.ExecuteAsync<PersonRow>(connection, source, null!));
    }

    [Fact]
    public async Task ExecuteAsync_WithWrongProvider_ThrowsArgumentException()
    {
        var engine = new SqlServerQueryExecutionEngine(executor: new FakeExecutor([], 0));
        var source = new QuerySource(ProviderName.Sqlite, "Employees");

        using var connection = new FakeConnection();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            engine.ExecuteAsync<PersonRow>(connection, source, new QueryRequest()));
        Assert.Contains("not valid for", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyTableName_ThrowsArgumentException()
    {
        var engine = new SqlServerQueryExecutionEngine(executor: new FakeExecutor([], 0));
        var source = new QuerySource(ProviderName.SqlServer, "   ");

        using var connection = new FakeConnection();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            engine.ExecuteAsync<PersonRow>(connection, source, new QueryRequest()));
        Assert.Contains("table", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WithGroupedLimit_BumpsFetchNextForHasMore()
    {
        var fakeExecutor = new FakeExecutor(
            rows: [
                new PersonRow { Name = "Engineering" },
                new PersonRow { Name = "Sales" },
                new PersonRow { Name = "HR" }
            ],
            count: 3);

        var engine = new SqlServerQueryExecutionEngine(executor: fakeExecutor);
        var source = new QuerySource(ProviderName.SqlServer, "Employees");

        using var connection = new FakeConnection();
        var request = new QueryRequest
        {
            Order = [new OrderClause { Field = "Name", Direction = "asc" }],
            Group = new GroupRequest
            {
                GroupBy = ["Department"],
                Metrics =
                [
                    new GroupMetricRequest { Operation = "count", Field = "*", Alias = "employees" }
                ]
            },
            Limit = 2,
            IncludeCount = false
        };

        var response = await engine.ExecuteAsync<PersonRow>(connection, source, request);

        Assert.Equal(2, response.Results.Count);
        Assert.True(response.HasMore);
        Assert.NotNull(fakeExecutor.LastRowsSql);
        Assert.Contains("FETCH NEXT 3 ROWS ONLY", fakeExecutor.LastRowsSql);
    }

    [Fact]
    public async Task ExecuteAsync_WithWrongKindContinuationToken_ThrowsArgumentException()
    {
        await AssertTokenPayloadRejectedAsync(
            """{"Kind":"not-sqlserver","OrderValues":[{"Field":"Age","Direction":"desc","Value":24}]}""",
            "Invalid continuation token payload");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyObjectProviderToken_ThrowsArgumentException()
    {
        await AssertTokenPayloadRejectedAsync("{}", "Invalid continuation token payload");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidJsonProviderToken_ThrowsArgumentException()
    {
        await AssertTokenPayloadRejectedAsync("{not-json", "Invalid continuation token payload");
    }

    [Fact]
    public async Task ExecuteAsync_WithOrderValuesCountMismatch_ThrowsArgumentException()
    {
        await AssertTokenPayloadRejectedAsync(
            """{"Kind":"sqlserver-seek-v1","OrderValues":[]}""",
            "order values mismatch");
    }

    [Fact]
    public async Task ExecuteAsync_WithOrderFieldMismatch_ThrowsArgumentException()
    {
        await AssertTokenPayloadRejectedAsync(
            """{"Kind":"sqlserver-seek-v1","OrderValues":[{"Field":"Name","Direction":"desc","Value":24}]}""",
            "order values mismatch");
    }

    [Fact]
    public async Task ExecuteAsync_WithOrderDirectionMismatch_ThrowsArgumentException()
    {
        await AssertTokenPayloadRejectedAsync(
            """{"Kind":"sqlserver-seek-v1","OrderValues":[{"Field":"Age","Direction":"asc","Value":24}]}""",
            "order values mismatch");
    }

    [Fact]
    public async Task ExecuteAsync_WithNonScalarOrderValue_ThrowsArgumentException()
    {
        await AssertTokenPayloadRejectedAsync(
            """{"Kind":"sqlserver-seek-v1","OrderValues":[{"Field":"Age","Direction":"desc","Value":{"x":1}}]}""",
            "non-scalar");
    }

    [Fact]
    public async Task ExecuteAsync_WithAscOrderContinuation_UsesGreaterThanSeek()
    {
        var fakeExecutor = new FakePagedExecutor(
            firstRows: [
                new PersonRow { Name = "Asha", Age = 19 },
                new PersonRow { Name = "Karan", Age = 22 },
                new PersonRow { Name = "Arun", Age = 24 }
            ],
            secondRows: [
                new PersonRow { Name = "Arun", Age = 24 },
                new PersonRow { Name = "Riya", Age = 31 }
            ],
            count: 4,
            seekMarker: "[t].[Age] >");

        var engine = new SqlServerQueryExecutionEngine(executor: fakeExecutor);
        var source = new QuerySource(ProviderName.SqlServer, "Employees");
        using var connection = new FakeConnection();

        var first = await engine.ExecuteAsync<PersonRow>(connection, source, new QueryRequest
        {
            Order = [new OrderClause { Field = "Age", Direction = "asc" }],
            Limit = 2
        });
        Assert.NotNull(first.ContinuationToken);

        var second = await engine.ExecuteAsync<PersonRow>(connection, source, new QueryRequest
        {
            Order = [new OrderClause { Field = "Age", Direction = "asc" }],
            Limit = 2,
            ContinuationToken = first.ContinuationToken
        });

        Assert.Equal(2, second.Results.Count);
        Assert.Contains("[t].[Age] >", fakeExecutor.LastRowsSql);
    }

    [Fact]
    public async Task ExecuteAsync_WithMultiFieldOrderContinuation_AppliesCompositeSeek()
    {
        var fakeExecutor = new FakePagedExecutor(
            firstRows: [
                new PersonRow { Name = "Asha", Age = 19 },
                new PersonRow { Name = "Karan", Age = 22 },
                new PersonRow { Name = "Arun", Age = 24 }
            ],
            secondRows: [
                new PersonRow { Name = "Arun", Age = 24 }
            ],
            count: 3,
            seekMarker: " OR ");

        var engine = new SqlServerQueryExecutionEngine(executor: fakeExecutor);
        var source = new QuerySource(ProviderName.SqlServer, "Employees");
        using var connection = new FakeConnection();

        var first = await engine.ExecuteAsync<PersonRow>(connection, source, new QueryRequest
        {
            Order =
            [
                new OrderClause { Field = "Age", Direction = "asc" },
                new OrderClause { Field = "Name", Direction = "asc" }
            ],
            Limit = 2
        });
        Assert.NotNull(first.ContinuationToken);

        await engine.ExecuteAsync<PersonRow>(connection, source, new QueryRequest
        {
            Order =
            [
                new OrderClause { Field = "Age", Direction = "asc" },
                new OrderClause { Field = "Name", Direction = "asc" }
            ],
            Limit = 2,
            ContinuationToken = first.ContinuationToken
        });

        Assert.Contains(" OR ", fakeExecutor.LastRowsSql);
        Assert.Contains("[t].[Age] =", fakeExecutor.LastRowsSql);
        Assert.Contains("[t].[Name] >", fakeExecutor.LastRowsSql);
    }

    [Fact]
    public async Task ExecuteAsync_WithContinuationTokenAndExistingWhere_MergesSeekFilter()
    {
        var fakeExecutor = new FakePagedExecutor(
            firstRows: [
                new PersonRow { Name = "Riya", Age = 31 },
                new PersonRow { Name = "Arun", Age = 24 },
                new PersonRow { Name = "Karan", Age = 22 }
            ],
            secondRows: [
                new PersonRow { Name = "Karan", Age = 22 }
            ],
            count: 3);

        var engine = new SqlServerQueryExecutionEngine(executor: fakeExecutor);
        var source = new QuerySource(ProviderName.SqlServer, "Employees");
        using var connection = new FakeConnection();
        var where = JsonDocument.Parse("""{"Department":"Engineering"}""").RootElement;

        var first = await engine.ExecuteAsync<PersonRow>(connection, source, new QueryRequest
        {
            Where = where,
            Order = [new OrderClause { Field = "Age", Direction = "desc" }],
            Limit = 2
        });
        Assert.NotNull(first.ContinuationToken);

        await engine.ExecuteAsync<PersonRow>(connection, source, new QueryRequest
        {
            Where = where,
            Order = [new OrderClause { Field = "Age", Direction = "desc" }],
            Limit = 2,
            ContinuationToken = first.ContinuationToken
        });

        Assert.Contains("[t].[Department] =", fakeExecutor.LastRowsSql);
        Assert.Contains("[t].[Age] <", fakeExecutor.LastRowsSql);
        Assert.Contains(" AND ", fakeExecutor.LastRowsSql);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOrderedFieldMissingOnRow_ThrowsInvalidOperationException()
    {
        var engine = new SqlServerQueryExecutionEngine(executor: new FakeNameOnlyExecutor());
        var source = new QuerySource(ProviderName.SqlServer, "Employees");
        using var connection = new FakeConnection();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.ExecuteAsync<NameOnlyRow>(connection, source, new QueryRequest
            {
                Order = [new OrderClause { Field = "Age", Direction = "asc" }],
                Limit = 1
            }));
        Assert.Contains("Unable to read ordered field", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithDictionaryRows_EncodesContinuationFromDictionaryKeys()
    {
        var fakeExecutor = new FakeDictionaryPagedExecutor();
        var engine = new SqlServerQueryExecutionEngine(executor: fakeExecutor);
        var source = new QuerySource(ProviderName.SqlServer, "Employees");
        using var connection = new FakeConnection();

        var first = await engine.ExecuteAsync<Dictionary<string, object?>>(connection, source, new QueryRequest
        {
            Order = [new OrderClause { Field = "Age", Direction = "desc" }],
            Limit = 2
        });

        Assert.NotNull(first.ContinuationToken);
        Assert.Equal(2, first.Results.Count);
    }

    private static async Task AssertTokenPayloadRejectedAsync(string providerToken, string messageFragment)
    {
        var protector = new Base64ContinuationTokenProtector();
        var fakeExecutor = new FakePagedExecutor(
            firstRows: [
                new PersonRow { Name = "Riya", Age = 31 },
                new PersonRow { Name = "Arun", Age = 24 },
                new PersonRow { Name = "Karan", Age = 22 }
            ],
            secondRows: [],
            count: 3);
        var engine = new SqlServerQueryExecutionEngine(executor: fakeExecutor, tokenProtector: protector);
        var source = new QuerySource(ProviderName.SqlServer, "Employees");
        using var connection = new FakeConnection();

        var first = await engine.ExecuteAsync<PersonRow>(connection, source, new QueryRequest
        {
            Order = [new OrderClause { Field = "Age", Direction = "desc" }],
            Limit = 2
        });
        Assert.True(protector.TryUnprotect(first.ContinuationToken!, out var envelope));

        var badToken = protector.Protect(new ContinuationTokenEnvelope
        {
            Provider = envelope!.Provider,
            QueryShapeHash = envelope.QueryShapeHash,
            ProviderToken = providerToken
        });

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            engine.ExecuteAsync<PersonRow>(connection, source, new QueryRequest
            {
                Order = [new OrderClause { Field = "Age", Direction = "desc" }],
                Limit = 2,
                ContinuationToken = badToken
            }));
        Assert.Contains(messageFragment, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class PersonRow
    {
        public string Name { get; init; } = string.Empty;
        public int Age { get; init; }
    }

    private sealed class NameOnlyRow
    {
        public string Name { get; init; } = string.Empty;
    }

    private sealed class FakeExecutor(
        IReadOnlyList<PersonRow> rows,
        long count) : ISqlServerQueryExecutor
    {
        private readonly IReadOnlyList<PersonRow> _rows = rows;
        private readonly long _count = count;

        public string? LastRowsSql { get; private set; }
        public string? LastCountSql { get; private set; }

        public Task<IReadOnlyList<T>> ExecuteRowsAsync<T>(
            IDbConnection connection,
            SqlServerSqlTranslationResult translation,
            CancellationToken cancellationToken = default)
        {
            LastRowsSql = translation.Sql;
            return Task.FromResult((IReadOnlyList<T>)_rows);
        }

        public Task<long> ExecuteCountAsync(
            IDbConnection connection,
            SqlServerSqlTranslationResult translation,
            CancellationToken cancellationToken = default)
        {
            LastCountSql = translation.Sql;
            return Task.FromResult(_count);
        }
    }

    private sealed class FakeNameOnlyExecutor : ISqlServerQueryExecutor
    {
        public Task<IReadOnlyList<T>> ExecuteRowsAsync<T>(
            IDbConnection connection,
            SqlServerSqlTranslationResult translation,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<NameOnlyRow> rows = [new NameOnlyRow { Name = "Asha" }, new NameOnlyRow { Name = "Riya" }];
            return Task.FromResult((IReadOnlyList<T>)rows);
        }

        public Task<long> ExecuteCountAsync(
            IDbConnection connection,
            SqlServerSqlTranslationResult translation,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(2L);
    }

    private sealed class FakePagedExecutor(
        IReadOnlyList<PersonRow> firstRows,
        IReadOnlyList<PersonRow> secondRows,
        long count,
        string seekMarker = "[t].[Age] <") : ISqlServerQueryExecutor
    {
        private readonly IReadOnlyList<PersonRow> _firstRows = firstRows;
        private readonly IReadOnlyList<PersonRow> _secondRows = secondRows;
        private readonly long _count = count;
        private readonly string _seekMarker = seekMarker;

        public string? LastRowsSql { get; private set; }
        public string? LastCountSql { get; private set; }

        public Task<IReadOnlyList<T>> ExecuteRowsAsync<T>(
            IDbConnection connection,
            SqlServerSqlTranslationResult translation,
            CancellationToken cancellationToken = default)
        {
            LastRowsSql = translation.Sql;

            var rows = translation.Sql.Contains(_seekMarker, StringComparison.Ordinal)
                || (_seekMarker == " OR " && translation.Sql.Contains(" OR ", StringComparison.Ordinal)
                    && translation.Sql.Contains("[t].[Age]", StringComparison.Ordinal)
                    && translation.Sql.Contains("WHERE", StringComparison.Ordinal))
                ? _secondRows
                : _firstRows;

            // For OR marker, first page has ORDER BY but no seek WHERE composite; second has OR
            if (_seekMarker == " OR ")
            {
                rows = translation.Sql.Contains(" OR (", StringComparison.Ordinal)
                    || (translation.Sql.Contains(" OR ", StringComparison.Ordinal)
                        && translation.Sql.Contains("[t].[Name]", StringComparison.Ordinal)
                        && translation.Sql.Contains("WHERE", StringComparison.Ordinal))
                    ? _secondRows
                    : _firstRows;
            }

            return Task.FromResult((IReadOnlyList<T>)rows);
        }

        public Task<long> ExecuteCountAsync(
            IDbConnection connection,
            SqlServerSqlTranslationResult translation,
            CancellationToken cancellationToken = default)
        {
            LastCountSql = translation.Sql;
            return Task.FromResult(_count);
        }
    }

    private sealed class FakeDictionaryPagedExecutor : ISqlServerQueryExecutor
    {
        private int _calls;

        public Task<IReadOnlyList<T>> ExecuteRowsAsync<T>(
            IDbConnection connection,
            SqlServerSqlTranslationResult translation,
            CancellationToken cancellationToken = default)
        {
            _calls++;
            IReadOnlyList<Dictionary<string, object?>> rows = _calls == 1
                ?
                [
                    new Dictionary<string, object?> { ["Name"] = "Riya", ["Age"] = 31 },
                    new Dictionary<string, object?> { ["Name"] = "Arun", ["Age"] = 24 },
                    new Dictionary<string, object?> { ["Name"] = "Karan", ["Age"] = 22 }
                ]
                :
                [
                    new Dictionary<string, object?> { ["Name"] = "Karan", ["Age"] = 22 }
                ];
            return Task.FromResult((IReadOnlyList<T>)rows);
        }

        public Task<long> ExecuteCountAsync(
            IDbConnection connection,
            SqlServerSqlTranslationResult translation,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(3L);
    }

    private sealed class FakeConnection : IDbConnection
    {
        [AllowNull]
        public string ConnectionString { get; set; } = string.Empty;
        public int ConnectionTimeout => 0;
        public string Database => "Fake";
        public ConnectionState State => ConnectionState.Open;

        public IDbTransaction BeginTransaction()
        {
            throw new NotSupportedException();
        }

        public IDbTransaction BeginTransaction(IsolationLevel il)
        {
            throw new NotSupportedException();
        }

        public void ChangeDatabase(string databaseName)
        {
        }

        public void Close()
        {
        }

        public IDbCommand CreateCommand()
        {
            throw new NotSupportedException();
        }

        public void Open()
        {
        }

        public void Dispose()
        {
        }
    }
}
