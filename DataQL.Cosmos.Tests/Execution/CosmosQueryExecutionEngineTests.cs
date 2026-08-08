using System.Text.Json;
using DataQL.Abstractions;
using DataQL.Contracts;
using DataQL.Cosmos;
using DataQL.Cosmos.Execution;
using DataQL.Cosmos.Translation;
using DataQL.Token;
using Microsoft.Azure.Cosmos;

namespace DataQL.Cosmos.Tests.Execution;

public class CosmosQueryExecutionEngineTests
{
    [Fact]
    public async Task ExecuteAsync_WithNullSession_ThrowsArgumentNullException()
    {
        var engine = new CosmosQueryExecutionEngine(executor: new FakeExecutor());
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            engine.ExecuteAsync<object>(null!, new QuerySource(ProviderName.Cosmos, "Employees"), new QueryRequest()));
    }

    [Fact]
    public async Task ExecuteAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        using var client = CreateClient();
        await using var session = new CosmosDataQLSession(client, "DataQL");
        var engine = new CosmosQueryExecutionEngine(executor: new FakeExecutor());
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            engine.ExecuteAsync<object>(session, new QuerySource(ProviderName.Cosmos, "Employees"), null!));
    }

    [Fact]
    public async Task ExecuteAsync_WithWrongProvider_ThrowsArgumentException()
    {
        using var client = CreateClient();
        await using var session = new CosmosDataQLSession(client, "DataQL");
        var engine = new CosmosQueryExecutionEngine(executor: new FakeExecutor());
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            engine.ExecuteAsync<object>(
                session,
                new QuerySource(ProviderName.Sqlite, "Employees"),
                new QueryRequest { Order = [new OrderClause { Field = "Age", Direction = "asc" }] }));
        Assert.Contains("not valid for", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyContainerName_ThrowsArgumentException()
    {
        using var client = CreateClient();
        await using var session = new CosmosDataQLSession(client, "DataQL");
        var engine = new CosmosQueryExecutionEngine(executor: new FakeExecutor());
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            engine.ExecuteAsync<object>(
                session,
                new QuerySource(ProviderName.Cosmos, "  "),
                new QueryRequest { Order = [new OrderClause { Field = "Age", Direction = "asc" }] }));
        Assert.Contains("container", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidContinuationToken_ThrowsArgumentException()
    {
        using var client = CreateClient();
        await using var session = new CosmosDataQLSession(client, "DataQL");
        var engine = new CosmosQueryExecutionEngine(executor: new FakeExecutor());
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            engine.ExecuteAsync<object>(
                session,
                new QuerySource(ProviderName.Cosmos, "Employees"),
                new QueryRequest
                {
                    Order = [new OrderClause { Field = "Age", Direction = "desc" }],
                    Limit = 2,
                    ContinuationToken = "not-a-valid-token"
                }));
        Assert.Contains("Invalid continuation token", ex.Message);
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

        using var client = CreateClient();
        await using var session = new CosmosDataQLSession(client, "DataQL");
        var engine = new CosmosQueryExecutionEngine(executor: new FakeExecutor(), tokenProtector: protector);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            engine.ExecuteAsync<object>(
                session,
                new QuerySource(ProviderName.Cosmos, "Employees"),
                new QueryRequest
                {
                    Order = [new OrderClause { Field = "Age", Direction = "desc" }],
                    Limit = 2,
                    ContinuationToken = token
                }));
        Assert.Contains("provider mismatch", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithShapeHashMismatchContinuationToken_ThrowsArgumentException()
    {
        var protector = new Base64ContinuationTokenProtector();
        var token = protector.Protect(new ContinuationTokenEnvelope
        {
            Provider = ProviderName.Cosmos,
            QueryShapeHash = "deadbeef",
            ProviderToken = """{"Kind":"cosmos-feed-v1","FeedToken":"x"}"""
        });

        using var client = CreateClient();
        await using var session = new CosmosDataQLSession(client, "DataQL");
        var engine = new CosmosQueryExecutionEngine(executor: new FakeExecutor(), tokenProtector: protector);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            engine.ExecuteAsync<object>(
                session,
                new QuerySource(ProviderName.Cosmos, "Employees"),
                new QueryRequest
                {
                    Order = [new OrderClause { Field = "Age", Direction = "desc" }],
                    Limit = 2,
                    ContinuationToken = token
                }));
        Assert.Contains("does not match request shape", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithWrongKindContinuationToken_ThrowsArgumentException()
    {
        await AssertTokenPayloadRejectedAsync(
            """{"Kind":"not-cosmos","FeedToken":"x"}""",
            "Invalid continuation token payload");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyFeedTokenPayload_ThrowsArgumentException()
    {
        await AssertTokenPayloadRejectedAsync(
            """{"Kind":"cosmos-feed-v1","FeedToken":"  "}""",
            "Invalid continuation token payload");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidJsonProviderToken_ThrowsArgumentException()
    {
        await AssertTokenPayloadRejectedAsync("{not-json", "Invalid continuation token payload");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidFeedToken_PassesTokenToExecutor()
    {
        var protector = new Base64ContinuationTokenProtector();
        var fake = new FakeExecutor();
        using var client = CreateClient();
        await using var session = new CosmosDataQLSession(client, "DataQL");
        var engine = new CosmosQueryExecutionEngine(executor: fake, tokenProtector: protector);

        var first = await engine.ExecuteAsync<PersonRow>(
            session,
            new QuerySource(ProviderName.Cosmos, "Employees"),
            new QueryRequest
            {
                Order = [new OrderClause { Field = "Age", Direction = "desc" }],
                Limit = 2
            });
        Assert.NotNull(first.ContinuationToken);
        Assert.True(protector.TryUnprotect(first.ContinuationToken!, out var envelope));

        var rewritten = protector.Protect(new ContinuationTokenEnvelope
        {
            Provider = envelope!.Provider,
            QueryShapeHash = envelope.QueryShapeHash,
            ProviderToken = """{"Kind":"cosmos-feed-v1","FeedToken":"page-2-token"}"""
        });

        await engine.ExecuteAsync<PersonRow>(
            session,
            new QuerySource(ProviderName.Cosmos, "Employees"),
            new QueryRequest
            {
                Order = [new OrderClause { Field = "Age", Direction = "desc" }],
                Limit = 2,
                ContinuationToken = rewritten
            });

        Assert.Equal("page-2-token", fake.LastFeedToken);
    }

    [Fact]
    public async Task ExecuteAsync_WithIncludeCount_SetsCountAndChargeExtensions()
    {
        var fake = new FakeExecutor();
        using var client = CreateClient();
        await using var session = new CosmosDataQLSession(client, "DataQL");
        var engine = new CosmosQueryExecutionEngine(executor: fake);

        var response = await engine.ExecuteAsync<PersonRow>(
            session,
            new QuerySource(ProviderName.Cosmos, "Employees"),
            new QueryRequest
            {
                Order = [new OrderClause { Field = "Age", Direction = "desc" }],
                Limit = 2,
                IncludeCount = true
            });

        Assert.Equal(4, response.Count);
        Assert.NotNull(response.Meta?.Extensions);
        Assert.True(response.Meta!.Extensions!.ContainsKey("requestCharge"));
        Assert.True(response.Meta.Extensions.ContainsKey("countRequestCharge"));
    }

    private static async Task AssertTokenPayloadRejectedAsync(string providerToken, string messageFragment)
    {
        var protector = new Base64ContinuationTokenProtector();
        var fake = new FakeExecutor();
        using var client = CreateClient();
        await using var session = new CosmosDataQLSession(client, "DataQL");
        var engine = new CosmosQueryExecutionEngine(executor: fake, tokenProtector: protector);

        var first = await engine.ExecuteAsync<PersonRow>(
            session,
            new QuerySource(ProviderName.Cosmos, "Employees"),
            new QueryRequest
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
            engine.ExecuteAsync<PersonRow>(
                session,
                new QuerySource(ProviderName.Cosmos, "Employees"),
                new QueryRequest
                {
                    Order = [new OrderClause { Field = "Age", Direction = "desc" }],
                    Limit = 2,
                    ContinuationToken = badToken
                }));
        Assert.Contains(messageFragment, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static CosmosClient CreateClient() =>
        new("https://localhost:8081/", Convert.ToBase64String(new byte[64]), new CosmosClientOptions
        {
            ConnectionMode = ConnectionMode.Gateway,
            LimitToEndpoint = true
        });

    private sealed class PersonRow
    {
        public string Name { get; init; } = string.Empty;
        public int Age { get; init; }
    }

    private sealed class FakeExecutor : ICosmosQueryExecutor
    {
        public string? LastFeedToken { get; private set; }

        public Task<CosmosQueryPageResult<T>> ExecutePageAsync<T>(
            Container container,
            CosmosSqlTranslationResult translation,
            int? maxItemCount,
            string? feedContinuationToken,
            CancellationToken cancellationToken = default)
        {
            LastFeedToken = feedContinuationToken;
            IReadOnlyList<PersonRow> items =
            [
                new PersonRow { Name = "Riya", Age = 31 },
                new PersonRow { Name = "Arun", Age = 24 }
            ];
            return Task.FromResult(new CosmosQueryPageResult<T>
            {
                Items = (IReadOnlyList<T>)items,
                ContinuationToken = string.IsNullOrWhiteSpace(feedContinuationToken) ? "next" : null,
                RequestCharge = 1.5
            });
        }

        public Task<CosmosCountResult> ExecuteCountAsync(
            Container container,
            CosmosSqlTranslationResult translation,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CosmosCountResult { Count = 4, RequestCharge = 0.5 });
    }
}
