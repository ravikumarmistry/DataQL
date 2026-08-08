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

        // Keep aligned with testdata/Employees.json.
        await UpsertEmployeeAsync(container, new EmployeeSeed(
            Id: "1",
            Name: "Asha",
            Age: 19,
            City: "Delhi",
            Department: "Engineering",
            IsActive: true,
            Notes: "junior",
            CreatedAt: "2025-01-10T10:00:00Z",
            Tags: ["junior", "remote"],
            Skills: ["C#", ".NET"],
            Address: new AddressSeed("Delhi", "India"),
            Projects: [new ProjectSeed("Alpha", "Active", 30)]));

        await UpsertEmployeeAsync(container, new EmployeeSeed(
            Id: "2",
            Name: "Arun",
            Age: 24,
            City: "Bengaluru",
            Department: "Engineering",
            IsActive: true,
            Notes: null,
            CreatedAt: "2025-01-11T10:00:00Z",
            Tags: ["senior"],
            Skills: ["Java", "Azure"],
            Address: new AddressSeed("Bengaluru", "India"),
            Projects: [new ProjectSeed("Beta", "Done", 10)]));

        await UpsertEmployeeAsync(container, new EmployeeSeed(
            Id: "3",
            Name: "Riya",
            Age: 31,
            City: "Delhi",
            Department: "Sales",
            IsActive: true,
            Notes: "lead",
            CreatedAt: "2025-01-12T10:00:00Z",
            Tags: ["lead", "remote", "sales"],
            Skills: ["Azure", ".NET", "SQL"],
            Address: new AddressSeed("Delhi", "India"),
            Projects:
            [
                new ProjectSeed("Gamma", "Active", 25),
                new ProjectSeed("Delta", "Active", 5)
            ]));

        await UpsertEmployeeAsync(container, new EmployeeSeed(
            Id: "4",
            Name: "Karan",
            Age: 22,
            City: "Pune",
            Department: "Engineering",
            IsActive: false,
            Notes: null,
            CreatedAt: "2025-01-13T10:00:00Z",
            Tags: [],
            Skills: [],
            Address: new AddressSeed("Pune", "India"),
            Projects: []));
    }

    public Task DisposeAsync()
    {
        Client?.Dispose();
        return Task.CompletedTask;
    }

    private static Task UpsertEmployeeAsync(Container container, EmployeeSeed seed)
    {
        // Omit null Notes so $exists:false matches documents without the property (Sqlite NULL semantics).
        object doc = seed.Notes is null
            ? new
            {
                id = seed.Id,
                Name = seed.Name,
                Age = seed.Age,
                City = seed.City,
                Department = seed.Department,
                IsActive = seed.IsActive,
                CreatedAt = seed.CreatedAt,
                Tags = seed.Tags,
                Skills = seed.Skills,
                Address = seed.Address,
                Projects = seed.Projects
            }
            : new
            {
                id = seed.Id,
                Name = seed.Name,
                Age = seed.Age,
                City = seed.City,
                Department = seed.Department,
                IsActive = seed.IsActive,
                Notes = seed.Notes,
                CreatedAt = seed.CreatedAt,
                Tags = seed.Tags,
                Skills = seed.Skills,
                Address = seed.Address,
                Projects = seed.Projects
            };
        return container.UpsertItemAsync(doc, new PartitionKey(seed.Id));
    }

    private sealed record AddressSeed(string City, string Country);

    private sealed record ProjectSeed(string Name, string Status, int Hours);

    private sealed record EmployeeSeed(
        string Id,
        string Name,
        int Age,
        string City,
        string Department,
        bool IsActive,
        string? Notes,
        string CreatedAt,
        string[] Tags,
        string[] Skills,
        AddressSeed Address,
        ProjectSeed[] Projects);
}

[CollectionDefinition(Name)]
public sealed class CosmosE2eCollection : ICollectionFixture<CosmosE2eFixture>
{
    public const string Name = "CosmosE2e";
}
