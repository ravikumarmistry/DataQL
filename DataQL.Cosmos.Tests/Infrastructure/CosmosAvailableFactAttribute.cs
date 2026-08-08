namespace DataQL.Cosmos.Tests.Infrastructure;

/// <summary>
/// Skips the test when the Cosmos emulator is unreachable.
/// </summary>
public sealed class CosmosAvailableFactAttribute : FactAttribute
{
    public CosmosAvailableFactAttribute()
    {
        if (!CosmosTestEnvironment.IsAvailable)
        {
            Skip =
                "Cosmos emulator is not available. Start DataQL.Cosmos.Tests/docker "
                + "(cp .env.example .env && docker compose up -d).";
        }
    }
}
