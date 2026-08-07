using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DataQL.Ast.Parsing;
using DataQL.Abstractions;
using DataQL.Ast.Model;
using DataQL.Contracts;
using DataQL.Pipeline;
using DataQL.SqlServer.Translation;
using DataQL.Token;
using DataQL.Validation;

namespace DataQL.SqlServer.Execution;

public sealed class SqlServerQueryExecutionEngine
{
    private const string SqlServerContinuationTokenKind = "sqlserver-seek-v1";

    private readonly QueryProcessor _processor;
    private readonly SqlServerSqlTranslator _translator;
    private readonly ISqlServerQueryExecutor _executor;
    private readonly IContinuationTokenProtector _tokenProtector;
    private readonly ProviderCapabilities _capabilities;
    private readonly IProviderCapabilityValidator _capabilityValidator;

    public SqlServerQueryExecutionEngine(
        QueryProcessor? processor = null,
        SqlServerSqlTranslator? translator = null,
        ISqlServerQueryExecutor? executor = null,
        IContinuationTokenProtector? tokenProtector = null,
        ProviderCapabilities? capabilities = null,
        IProviderCapabilityValidator? capabilityValidator = null)
    {
        _processor = processor ?? new QueryProcessor(
            new QueryRequestValidator(),
            new QueryAstParser(),
            new AstSemanticValidator());
        _translator = translator ?? new SqlServerSqlTranslator();
        _executor = executor ?? new SqlServerQueryExecutor();
        _tokenProtector = tokenProtector ?? new Base64ContinuationTokenProtector();
        _capabilities = capabilities ?? new SqlServerQueryTranslator().Capabilities;
        _capabilityValidator = capabilityValidator ?? ProviderCapabilityValidator.Instance;
    }

    public async Task<QueryResponse<T>> ExecuteAsync<T>(
        IDbConnection connection,
        QuerySource source,
        QueryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        ValidateSource(source);

        var ast = _processor.Process(request);
        _capabilityValidator.EnsureValid(ast, _capabilities);
        var tableName = source.Name;
        var queryShapeHash = BuildQueryShapeHash(tableName, request);
        var tokenPayload = DecodeSeekTokenOrThrow(request.ContinuationToken, queryShapeHash, request.Order);
        if (tokenPayload is not null)
        {
            var seekFilter = BuildSeekFilterOrThrow(request.Order, tokenPayload);
            var mergedWhere = ast.Where is null
                ? seekFilter
                : new LogicalFilter(FilterLogicalOperator.And, new[] { ast.Where, seekFilter });

            ast = ast with { Where = mergedWhere };
        }
        var translation = _translator.Translate(ast, tableName);

        long? count = null;
        if (request.IncludeCount)
        {
            var countTranslation = translation;
            if (request.Limit is > 0)
            {
                var countAst = ast with
                {
                    Order = [],
                    Pagination = ast.Pagination with
                    {
                        Limit = null,
                        IncludeCount = false
                    }
                };
                countTranslation = _translator.Translate(countAst, tableName);
            }

            count = await _executor.ExecuteCountAsync(connection, countTranslation, cancellationToken);
        }

        var rowTranslation = translation;
        var expectedLimit = request.Limit;
        if (expectedLimit is > 0)
        {
            rowTranslation = ApplyLimitPlusOne(translation, expectedLimit.Value);
        }

        var rows = await _executor.ExecuteRowsAsync<T>(connection, rowTranslation, cancellationToken);

        var hasMore = false;
        if (expectedLimit is > 0 && rows.Count > expectedLimit.Value)
        {
            hasMore = true;
            rows = TrimToLimit(rows, expectedLimit.Value);
        }

        string? continuationToken = null;
        if (hasMore && expectedLimit is > 0)
        {
            var lastItem = rows[rows.Count - 1];
            continuationToken = EncodeSeekToken(queryShapeHash, request.Order, lastItem);
        }

        return new QueryResponse<T>
        {
            Results = rows,
            HasMore = hasMore,
            ContinuationToken = continuationToken,
            Count = count
        };
    }

    private SqlServerContinuationPayload? DecodeSeekTokenOrThrow(
        string? continuationToken,
        string expectedQueryShapeHash,
        IReadOnlyList<OrderClause> order)
    {
        if (string.IsNullOrWhiteSpace(continuationToken))
        {
            return null;
        }

        if (!_tokenProtector.TryUnprotect(continuationToken, out var envelope) || envelope is null)
        {
            throw new ArgumentException("Invalid continuation token.", nameof(continuationToken));
        }

        if (!string.Equals(envelope.Provider, ProviderName.SqlServer, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Continuation token provider mismatch.", nameof(continuationToken));
        }

        if (!string.Equals(envelope.QueryShapeHash, expectedQueryShapeHash, StringComparison.Ordinal))
        {
            throw new ArgumentException("Continuation token does not match request shape.", nameof(continuationToken));
        }

        try
        {
            var payload = JsonSerializer.Deserialize<SqlServerContinuationPayload>(envelope.ProviderToken);
            if (payload is null || !string.Equals(payload.Kind, SqlServerContinuationTokenKind, StringComparison.Ordinal))
            {
                throw new ArgumentException("Invalid continuation token payload.", nameof(continuationToken));
            }

            if (payload.OrderValues.Count != order.Count)
            {
                throw new ArgumentException("Continuation token order values mismatch.", nameof(continuationToken));
            }

            for (var i = 0; i < order.Count; i++)
            {
                var expectedField = order[i].Field?.Trim() ?? string.Empty;
                var expectedDirection = order[i].Direction?.Trim().ToLowerInvariant() ?? "asc";
                var actual = payload.OrderValues[i];

                if (!string.Equals(actual.Field, expectedField, StringComparison.Ordinal)
                    || !string.Equals(actual.Direction, expectedDirection, StringComparison.Ordinal))
                {
                    throw new ArgumentException("Continuation token order values mismatch.", nameof(continuationToken));
                }
            }

            return payload;
        }
        catch (JsonException)
        {
            throw new ArgumentException("Invalid continuation token payload.", nameof(continuationToken));
        }
    }

    private string EncodeSeekToken<T>(string queryShapeHash, IReadOnlyList<OrderClause> order, T lastItem)
    {
        var orderValues = new List<SqlServerOrderValuePayload>(order.Count);
        foreach (var sort in order)
        {
            var field = sort.Field?.Trim() ?? string.Empty;
            if (!TryReadFieldValue(lastItem, field, out var value))
            {
                throw new InvalidOperationException("Unable to read ordered field value from result row for continuation token: " + field);
            }

            var direction = sort.Direction?.Trim().ToLowerInvariant() ?? "asc";
            orderValues.Add(new SqlServerOrderValuePayload
            {
                Field = field,
                Direction = direction,
                Value = JsonSerializer.SerializeToElement(value)
            });
        }

        var envelope = new ContinuationTokenEnvelope
        {
            Provider = ProviderName.SqlServer,
            QueryShapeHash = queryShapeHash,
            ProviderToken = JsonSerializer.Serialize(new SqlServerContinuationPayload
            {
                Kind = SqlServerContinuationTokenKind,
                OrderValues = orderValues
            })
        };

        return _tokenProtector.Protect(envelope);
    }

    private static string BuildQueryShapeHash(string tableName, QueryRequest request)
    {
        var signature = new
        {
            source = tableName,
            where = request.Where?.GetRawText(),
            order = request.Order.Select(o => new
            {
                field = (o.Field ?? string.Empty).Trim(),
                direction = (o.Direction ?? "asc").Trim().ToLowerInvariant()
            }),
            select = request.Select.Select(static s => s.Trim()),
            exclude = request.Exclude.Select(static s => s.Trim()),
            distinct = request.Distinct.Select(static s => s.Trim()),
            includeCount = request.IncludeCount,
            group = request.Group
        };

        var json = JsonSerializer.Serialize(signature);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }

    private static FilterExpression BuildSeekFilterOrThrow(IReadOnlyList<OrderClause> order, SqlServerContinuationPayload payload)
    {
        var disjuncts = new List<FilterExpression>(order.Count);

        for (var i = 0; i < order.Count; i++)
        {
            var andParts = new List<FilterExpression>(i + 1);
            for (var j = 0; j < i; j++)
            {
                andParts.Add(BuildComparison(order[j].Field, FieldOperator.Eq, payload.OrderValues[j].Value));
            }

            var direction = (order[i].Direction ?? "asc").Trim().ToLowerInvariant();
            var op = direction == "desc" ? FieldOperator.Lt : FieldOperator.Gt;
            andParts.Add(BuildComparison(order[i].Field, op, payload.OrderValues[i].Value));

            disjuncts.Add(andParts.Count == 1
                ? andParts[0]
                : new LogicalFilter(FilterLogicalOperator.And, andParts));
        }

        if (disjuncts.Count == 1)
        {
            return disjuncts[0];
        }

        return new LogicalFilter(FilterLogicalOperator.Or, disjuncts);
    }

    private static FieldFilter BuildComparison(string field, FieldOperator op, JsonElement value)
    {
        return new FieldFilter(
            new FieldPath(field),
            new[]
            {
                new ScalarOperation(op, ToAstValue(value))
            });
    }

    private static AstValue ToAstValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => new ScalarValue(null),
            JsonValueKind.True => new ScalarValue(true),
            JsonValueKind.False => new ScalarValue(false),
            JsonValueKind.Number when element.TryGetInt64(out var l) => new ScalarValue(l),
            JsonValueKind.Number when element.TryGetDecimal(out var d) => new ScalarValue(d),
            JsonValueKind.Number => new ScalarValue(element.GetDouble()),
            JsonValueKind.String => new ScalarValue(element.GetString()),
            _ => throw new ArgumentException("Continuation token contains non-scalar value.")
        };
    }

    private static bool TryReadFieldValue<T>(T row, string field, out object? value)
    {
        if (row is null)
        {
            value = null;
            return false;
        }

        if (row is IReadOnlyDictionary<string, object?> readOnlyDict)
        {
            foreach (var pair in readOnlyDict)
            {
                if (string.Equals(pair.Key, field, StringComparison.OrdinalIgnoreCase))
                {
                    value = pair.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        if (row is IDictionary<string, object?> dict)
        {
            foreach (var pair in dict)
            {
                if (string.Equals(pair.Key, field, StringComparison.OrdinalIgnoreCase))
                {
                    value = pair.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        var property = typeof(T).GetProperties()
            .FirstOrDefault(p => string.Equals(p.Name, field, StringComparison.OrdinalIgnoreCase));

        if (property is null)
        {
            value = null;
            return false;
        }

        value = property.GetValue(row);
        return true;
    }

    private static void ValidateSource(QuerySource source)
    {
        if (!string.Equals(source.Provider, ProviderName.SqlServer, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Source provider '{source.Provider}' is not valid for '{ProviderName.SqlServer}'.", nameof(source));
        }

        if (string.IsNullOrWhiteSpace(source.Name))
        {
            throw new ArgumentException("Source name (table) is required for SQL Server execution.", nameof(source));
        }
    }

    private static SqlServerSqlTranslationResult ApplyLimitPlusOne(
        SqlServerSqlTranslationResult translation,
        int limit)
    {
        var targetTop = "TOP (" + limit + ")";
        var replacementTop = "TOP (" + (limit + 1) + ")";

        if (translation.Sql.Contains(targetTop, StringComparison.Ordinal))
        {
            return new SqlServerSqlTranslationResult
            {
                Sql = translation.Sql.Replace(targetTop, replacementTop, StringComparison.Ordinal),
                Parameters = new Dictionary<string, object?>(translation.Parameters)
            };
        }

        return translation;
    }

    private static IReadOnlyList<T> TrimToLimit<T>(IReadOnlyList<T> rows, int limit)
    {
        var trimmed = new List<T>(limit);
        for (var i = 0; i < limit; i++)
        {
            trimmed.Add(rows[i]);
        }

        return trimmed;
    }

    private sealed class SqlServerContinuationPayload
    {
        public string Kind { get; init; } = string.Empty;
        public IReadOnlyList<SqlServerOrderValuePayload> OrderValues { get; init; } = [];
    }

    private sealed class SqlServerOrderValuePayload
    {
        public string Field { get; init; } = string.Empty;
        public string Direction { get; init; } = string.Empty;
        public JsonElement Value { get; init; }
    }
}
