# Cosmos DB emulator for DataQL.Cosmos.Tests

Independent stack owned by this test project (source key `p_cosmos`).

```bash
cd DataQL.Cosmos.Tests/docker
cp .env.example .env
docker compose up -d
# wait until healthy, then:
dotnet test ..
```

Uses the Linux Cosmos emulator (`vnext-preview`) on port **8081** with the well-known emulator key from `.env.example`.

The test fixture creates database `DataQL`, container `Employees`, and seeds sample documents (aligned with `testdata/Employees.json`).
