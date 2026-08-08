# Cosmos DB emulator for DataQL.Cosmos.Tests

Independent stack owned by this test project (source key `p_cosmos`).

```bash
cd DataQL.Cosmos.Tests/docker
cp .env.example .env
docker compose up -d
# wait until healthy, then:
dotnet test ..
```

Without this stack (or `DATAQL_COSMOS_ENDPOINT` / `DATAQL_COSMOS_KEY`), Cosmos E2E
tests marked `[CosmosAvailableFact]` are skipped. Unit tests in the same project
still run. Phase 1 CI does not start Docker, so those E2E tests soft-skip there.

Uses the Linux Cosmos emulator (`vnext-preview`) on port **8081** with the well-known emulator key from `.env.example`.

The test fixture (`CosmosE2eFixture`) owns seeding: it creates database `DataQL`, container `Employees`, and upserts sample documents (aligned with `testdata/Employees.json`). Docker Compose only starts the empty emulator.
