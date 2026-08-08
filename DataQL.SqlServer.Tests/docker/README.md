# SQL Server for DataQL.SqlServer.Tests

Independent stack owned by this test project (source key `p_sqlserver`).

```bash
cd DataQL.SqlServer.Tests/docker
cp .env.example .env
docker compose up -d
dotnet test ..
```

Without this stack (or `DATAQL_SQLSERVER_CONNECTION`), SQL Server E2E tests
marked `[SqlServerAvailableFact]` are skipped. Unit tests in the same project
still run. Phase 1 CI does not start Docker, so those E2E tests soft-skip there.

Settings live in `.env.example`. Tests load `.env` (preferred) or `.env.example`.

Init seeds `dbo.Employees` from `testdata/Employees.json`, including JSON columns
`Tags`, `Skills`, `Address`, and `Projects` for array/object filter operators.

```bash
docker compose down        # keep volume
docker compose down -v     # wipe and re-seed next up
```
