# DataQL

**DataQL** is a provider-independent JSON query DSL for .NET. Clients express query intent once — filters, projection, ordering, grouping, and cursor pagination — and DataQL validates, translates, and executes against the registered backend.

Use it when you want a single query contract across relational and document stores, without exposing SQL or provider-specific syntax to API consumers.

```text
Client  →  JSON Query DSL  →  Validate / AST  →  Provider Translator  →  Native Query  →  Results
```

| | |
|---|---|
| **Target** | .NET 8 |
| **License** | [MIT](LICENSE) |
| **Packages** | NuGet (`DataQL`, `DataQL.AspNetCore`, provider packages) |
| **Spec** | [Generic Query DSL v1.0](specs/Generic-Query-DSL-Specification-v1.0.md) |

---

## Packages

| Package | Role |
|---------|------|
| `DataQL` | Core DSL, validation, AST, hosting (`IDataQLService`) |
| `DataQL.AspNetCore` | HTTP endpoints, OpenAPI metadata |
| `DataQL.Sqlite` | SQLite provider |
| `DataQL.SqlServer` | SQL Server provider |
| `DataQL.Cosmos` | Azure Cosmos DB provider |

---

## Providers and compatibility

Capabilities are advertised per provider and enforced at validation time. Unsupported operators or features return structured capability errors (for example `Capability.OperatorNotSupported`).

### Feature matrix

| Feature | SQLite | SQL Server | Cosmos DB |
|---------|:------:|:----------:|:---------:|
| Select (projection) | Yes | Yes | Yes |
| Exclude | Yes | Yes | No |
| Distinct | Yes | Yes | No |
| Nested fields (`a.b`) | No | Yes | Yes |
| Grouping | Yes | Yes | Yes* |
| Having | Yes | Yes | No |
| Include count | Yes | Yes | Yes† |
| Cursor pagination | Seek / keyset | Seek / keyset | Feed token‡ |

\* Cosmos grouped queries return the full aggregate set: `limit` is ignored and continuation is not supported.  
† Cosmos `includeCount` runs a separate COUNT query (extra RU cost).  
‡ On Cosmos, `order` is optional unless a continuation token is supplied. On SQLite and SQL Server, `order` is **required** on every query for deterministic paging.

### Filter operators

| Operator | SQLite | SQL Server | Cosmos DB |
|----------|:------:|:----------:|:---------:|
| `$eq` `$ne` `$gt` `$gte` `$lt` `$lte` | Yes | Yes | Yes |
| `$in` `$nin` | Yes | Yes | Yes |
| `$contains` `$startsWith` `$endsWith` | Yes | Yes | Yes |
| `$regex` | No | No | Yes |
| `$exists` `$isNull` | Yes | Yes | Yes |
| `$and` `$or` `$not` | Yes | Yes | Yes |
| `$containsAny` `$containsAll` `$size` `$isEmpty` `$any` | No | Yes | Yes |

### Group metric operations

| Operation | SQLite | SQL Server | Cosmos DB |
|-----------|:------:|:----------:|:---------:|
| `count` `sum` `avg` `min` `max` | Yes | Yes | Yes |
| `first` `last` | No | No | No |

### Provider notes

| Provider | Highlights |
|----------|------------|
| **SQLite** | Relational tables; seek paging; grouping + having; no nested field paths |
| **SQL Server** | Nested fields and array operators; seek paging; grouping + having |
| **Cosmos DB** | Document model + nested fields; feed-token paging; no `exclude` / `distinct` / `having` |

At runtime you can also inspect capabilities via meta endpoints (see [How to use](#how-to-use)).

---

## How to use

### 1. Install packages

```bash
dotnet add package DataQL
dotnet add package DataQL.AspNetCore
dotnet add package DataQL.Sqlite      # and/or SqlServer / Cosmos
```

### 2. Register sources

```csharp
using DataQL;
using DataQL.AspNetCore;
using DataQL.Sqlite.DependencyInjection;
using Microsoft.Data.Sqlite;

builder.Services
    .AddDataQL(options =>
    {
        options.AddSqliteSource("employees", _ =>
            new SqliteConnection("Data Source=app.db"));

        // options.AddSqlServerSource("hr", _ => new SqlConnection(cs));
        // options.AddCosmosSource("docs", endpoint, key, databaseId);
    })
    .AddDataQLOpenApi();
```

### 3. Map endpoints (ASP.NET Core)

```csharp
var app = builder.Build();
app.MapDataQL("/dataql");
app.Run();
```

This exposes:

| Method | Route | Purpose |
|--------|-------|---------|
| `POST` | `/dataql/query/{sourceKey}/{table}` | Execute a query |
| `GET` | `/dataql/meta/sources` | List registered sources |
| `GET` | `/dataql/meta/sources/{sourceKey}/tables` | List tables / containers |
| `GET` | `/dataql/meta/sources/{sourceKey}/tables/{table}/schema` | Table schema |
| `GET` | `/dataql/meta/openapi.json` | Generated OpenAPI document |

### 4. Call the query API

**Request** — `POST /dataql/query/employees/Employees`

```json
{
  "where": {
    "$and": [
      { "City": "Delhi" },
      { "Age": { "$gte": 18 } }
    ]
  },
  "order": [
    { "field": "Id", "direction": "asc" }
  ],
  "select": ["Id", "Name", "Age", "City"],
  "limit": 50,
  "includeCount": false
}
```

**Response**

```json
{
  "results": [ /* rows */ ],
  "hasMore": true,
  "continuationToken": "...",
  "count": null
}
```

Pass `continuationToken` on the next request (same filter/order/select shape) to page forward. Offset/skip pagination is not supported.

### 5. Execute from code

```csharp
var response = await dataQLService.ExecuteAsync<object>(
    sourceKey: "employees",
    table: "Employees",
    request: queryRequest,
    cancellationToken);
```

You can also harden queries before execution — for example force `IsActive = true` with `QueryFilterBuilder` — as shown in `DataQL.ExampleApi`.

### Query shape (reference)

| Field | Description |
|-------|-------------|
| `where` | Filter tree (equality shorthand or `$` operators) |
| `order` | Sort clauses (`field` + `asc` / `desc`) |
| `select` / `exclude` | Projection |
| `distinct` | Distinct field list (provider-dependent) |
| `limit` | Page size |
| `continuationToken` | Opaque cursor from a previous page |
| `includeCount` | Optionally return total matching count |
| `group` | `groupBy`, `metrics`, optional `having` |

Full grammar and examples: [specs/Generic-Query-DSL-Specification-v1.0.md](specs/Generic-Query-DSL-Specification-v1.0.md).

### Example API

The solution includes `DataQL.ExampleApi`, a runnable sample with SQLite seed data and optional SQL Server / Cosmos wiring:

```bash
dotnet run --project DataQL.ExampleApi
```

---

## License

MIT — see [LICENSE](LICENSE).

Versioning and release process: [docs/release-process.md](docs/release-process.md).
