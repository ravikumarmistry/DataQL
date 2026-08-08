using DataQL.Cosmos.Tests.Infrastructure;
using Microsoft.Azure.Cosmos;

namespace DataQL.Cosmos.Tests.Execution;

/// <summary>
/// Ensures DataQL database/container exist and Employees sample docs are seeded.
/// </summary>
public sealed class CosmosE2eFixture : IAsyncLifetime
{
    public CosmosClient Client { get; private set; } = null!;

    public string DatabaseId { get; private set; } = string.Empty;

    public string ContainerId { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        if (!CosmosTestEnvironment.IsAvailable)
        {
            return;
        }

        DatabaseId = CosmosTestEnvironment.DatabaseId;
        ContainerId = CosmosTestEnvironment.ContainerId;
        Client = CosmosTestEnvironment.CreateClient();

        var databaseResponse = await Client.CreateDatabaseIfNotExistsAsync(DatabaseId);
        var database = databaseResponse.Database;
        var containerResponse = await database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(ContainerId, partitionKeyPath: "/id"));
        var container = containerResponse.Container;

        await UpsertEmployeeAsync(container, "1", "Asha", 19, "Delhi", "Engineering", true, "junior", "2025-01-10T10:00:00Z");
        await UpsertEmployeeAsync(container, "2", "Arun", 24, "Bengaluru", "Engineering", true, null, "2025-01-11T10:00:00Z");
        await UpsertEmployeeAsync(container, "3", "Riya", 31, "Delhi", "Sales", true, "lead", "2025-01-12T10:00:00Z");
        await UpsertEmployeeAsync(container, "4", "Karan", 22, "Pune", "Engineering", false, null, "2025-01-13T10:00:00Z");
    }

    public Task DisposeAsync()
    {
        Client?.Dispose();
        return Task.CompletedTask;
    }

    private static Task UpsertEmployeeAsync(
        Container container,
        string id,
        string name,
        int age,
        string city,
        string department,
        bool isActive,
        string? notes,
        string createdAt)
    {
        var doc = new
        {
            id,
            Name = name,
            Age = age,
            City = city,
            Department = department,
            IsActive = isActive,
            Notes = notes,
            CreatedAt = createdAt
        };
        return container.UpsertItemAsync(doc, new PartitionKey(id));
    }
}

[CollectionDefinition(Name)]
public sealed class CosmosE2eCollection : ICollectionFixture<CosmosE2eFixture>
{
    public const string Name = "CosmosE2e";
}
