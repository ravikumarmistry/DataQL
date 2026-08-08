namespace DataQL.SqlServer.Tests.Infrastructure;

/// <summary>
/// Skips the test when SQL Server is unreachable (e.g. docker stack not running).
/// </summary>
public sealed class SqlServerAvailableFactAttribute : FactAttribute
{
    public SqlServerAvailableFactAttribute()
    {
        if (!SqlServerTestEnvironment.IsAvailable)
        {
            Skip =
                "SQL Server is not available. Start DataQL.SqlServer.Tests/docker "
                + "(cp .env.example .env && docker compose up -d) "
                + "or set " + SqlServerTestEnvironment.ConnectionEnvironmentVariable + ".";
        }
    }
}
