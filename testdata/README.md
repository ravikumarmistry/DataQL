# Shared seed datasets

JSON files here document the sample rows used across providers.

| File | Applied by |
|------|------------|
| `Employees.json` | SqlServer docker init scripts; Cosmos e2e/ExampleApi seed upserts; Sqlite inline SQL |

Includes scalar columns plus JSON fields used by SqlServer (and Cosmos) operators:
`tags` / `skills` (arrays), `address` (object), `projects` (array of objects).

**SQL Server / Cosmos:** each consumer owns its compose stack.  
**Sqlite tests / ExampleApi (local file DB):** inline SQL kept aligned with this file (JSON columns stored as text; Sqlite does not support `$any` / `$size` / `$contains*`).
