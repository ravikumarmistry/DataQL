# Cosmos DB for DataQL.ExampleApi

Source key: `p_cosmos`.

```bash
cd DataQL.ExampleApi/docker-cosmos
cp .env.example .env
docker compose up -d
dotnet run --project ..
```

Creates/uses database `DataQL`. Seed containers via tests or app bootstrap. Emulator well-known key is in `.env.example`.
