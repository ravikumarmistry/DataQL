# SQL Server for DataQL.ExampleApi

Independent stack owned by the example app (source key `p_sqlserver`).
Default host port is **1434** so it can run next to the test stack (1433).

```bash
cd DataQL.ExampleApi/docker
cp .env.example .env
docker compose up -d
dotnet run --project ..
```

Optional override: `ConnectionStrings:SqlServer` or `DATAQL_SQLSERVER_CONNECTION`.
