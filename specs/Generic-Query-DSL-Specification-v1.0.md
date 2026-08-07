# Generic Query DSL Specification v1.0

## 1. Purpose

Design a provider-independent JSON Query DSL for ASP.NET Core capable of
querying relational and document databases through a single request
format.

Supported providers include:

-   EF Core
-   SQL Server
-   Cosmos DB
-   MongoDB
-   PostgreSQL (including JSON)
-   Future providers

The DSL expresses **query intent**, never provider-specific syntax.

------------------------------------------------------------------------

# 2. Design Principles

-   Provider independent
-   Document-first data model
-   Strong validation before execution
-   Provider-neutral AST
-   Extensible operators
-   Async execution
-   Cursor-based pagination only
-   Deterministic behavior across providers

------------------------------------------------------------------------

# 3. Architecture

``` text
Client
   │
JSON Query DSL
   │
Deserializer
   │
Validator
   │
Abstract Syntax Tree (AST)
   │
Provider Translator
   ├── EF Core
   ├── SQL Server
   ├── Cosmos DB
   ├── MongoDB
   └── Others
   │
Native Query
   │
Execution
```

------------------------------------------------------------------------

# 4. Document Model

Entities are treated as JSON documents.

Supported value types:

-   Primitive values
-   Nested objects
-   Arrays of primitives
-   Arrays of objects
-   Arbitrary nesting depth (configurable)

Example

``` json
{
  "id":"1001",
  "name":"John",
  "address":{
    "city":"Delhi",
    "country":"India"
  },
  "skills":[
    "Azure",
    ".NET"
  ],
  "projects":[
    {
      "name":"Portal",
      "status":"Completed",
      "hours":120
    },
    {
      "name":"Mobile",
      "status":"Active",
      "hours":40
    }
  ]
}
```

------------------------------------------------------------------------

# 5. Property Paths

Nested fields use dot notation.

Examples

``` text
name
address.city
projects.status
metadata.createdBy.id
```

Provider translations

  Provider    Translation
  ----------- ----------------------------------
  EF Core     x.Address.City
  SQL         JSON_VALUE(...) or mapped column
  Cosmos DB   c.address.city
  MongoDB     address.city

------------------------------------------------------------------------

# 6. Root Request

``` json
{
  "where": {},
  "order": [],
  "select": [],
  "exclude": [],
  "distinct": [],
  "limit": 100,
  "continuationToken": null,
  "includeCount": false,
  "group": {
    "groupBy": [],
    "metrics": []
  }
}
```

------------------------------------------------------------------------

# 7. Filtering

Simple equality

``` json
{
  "where":{
    "address.city":"Delhi"
  }
}
```

Comparison

``` json
{
  "where":{
    "age":{
      "$gte":18,
      "$lt":60
    }
  }
}
```

Logical

``` json
{
  "where":{
    "$and":[
      {
        "$or":[
          {"city":"Delhi"},
          {"city":"Mumbai"}
        ]
      },
      {
        "active":true
      }
    ]
  }
}
```

Unlimited nesting supported.

------------------------------------------------------------------------

# 8. Supported Operators

## Comparison

-   \$eq
-   \$ne
-   \$gt
-   \$gte
-   \$lt
-   \$lte

## Collections

-   \$in
-   \$nin

## Strings

-   \$contains
-   \$startsWith
-   \$endsWith
-   \$regex

## Boolean

-   \$and
-   \$or
-   \$not

## Null

-   \$exists
-   \$isNull

------------------------------------------------------------------------

# 9. Array Support

## Primitive Arrays

Supported operators

-   \$contains
-   \$containsAny
-   \$containsAll
-   \$size
-   \$isEmpty

Example

``` json
{
  "where":{
    "skills":{
      "$containsAll":[
        "Azure",
        ".NET"
      ]
    }
  }
}
```

## Arrays of Objects

Provider-neutral operator

``` json
{
  "where":{
    "projects":{
      "$any":{
        "status":"Active",
        "hours":{
          "$gt":40
        }
      }
    }
  }
}
```

Translations

-   EF Core → Any(...)
-   Cosmos DB → EXISTS(...)
-   MongoDB → \$elemMatch

------------------------------------------------------------------------

# 10. Projection

``` json
{
  "select":[
    "name",
    "address.city",
    "projects.name"
  ]
}
```

Fields may be excluded using `exclude`.

------------------------------------------------------------------------

# 11. Ordering

``` json
{
  "order":[
    {
      "field":"createdAt",
      "direction":"desc"
    }
  ]
}
```

Deterministic ordering is required.

Framework may append a unique identifier automatically.

------------------------------------------------------------------------

# 12. Cursor-Based Pagination

Offset/Skip pagination is **not supported**.

Request

``` json
{
  "limit":50,
  "continuationToken":"..."
}
```

Response

``` json
{
  "results":[],
  "hasMore":true,
  "continuationToken":"...",
  "count":null
}
```

Continuation tokens are:

-   Opaque
-   Immutable
-   Provider generated
-   Query-shape specific
-   Signed/encrypted by framework when applicable

Changing the query invalidates the token.

------------------------------------------------------------------------

# 13. Group Aggregation

Aggregation is intentionally simplified to a single `group` object.

Example

``` json
{
  "group":{
    "groupBy":[
      "department",
      "city"
    ],
    "metrics":[
      {
        "field":"salary",
        "operation":"avg",
        "alias":"averageSalary"
      },
      {
        "field":"salary",
        "operation":"sum",
        "alias":"totalSalary"
      },
      {
        "field":"*",
        "operation":"count",
        "alias":"employees"
      }
    ]
  }
}
```

Filtering remains in `where` and applies before grouping.

Ordering and limiting grouped results reuse existing root fields:

-   `order` sorts grouped output.
-   `limit` limits grouped rows.

No aggregation pipeline is used.

Supported group metric operations

-   `count`
-   `sum`
-   `avg`
-   `min`
-   `max`
-   `first`
-   `last`

------------------------------------------------------------------------

# 14. Validation

The validator shall reject:

-   Unknown fields
-   Unknown operators
-   Invalid types
-   Invalid continuation tokens
-   Excessive nesting
-   Unsupported provider capabilities
-   Excessive limits
-   Invalid group definitions

HTTP 400 shall be returned for validation failures.

------------------------------------------------------------------------

# 15. Provider Capability Model

Each provider declares supported features.

Examples:

-   Nested objects
-   Arrays
-   \$any
-   Regex
-   Group operations (`count`, `sum`, `avg`, `min`, `max`, `first`, `last`)

Unsupported operations fail validation before execution.

------------------------------------------------------------------------

# 16. Internal Object Model

``` text
QueryRequest
├── FilterNode
├── SortDefinition[]
├── Projection
├── GroupDefinition
├── ContinuationToken
└── Limit
```

Filter AST

``` text
FilterNode
├── ComparisonNode
├── LogicalNode
├── CollectionNode
├── ExistsNode
├── RegexNode
└── ConstantNode
```

------------------------------------------------------------------------

# 17. Translation Pipeline

``` text
JSON DSL
    ↓
Validation
    ↓
AST
    ↓
Provider Translator
    ↓
Native Query
```

Examples

-   AST → LINQ Expression
-   AST → Cosmos Query
-   AST → SQL GROUP BY projection

------------------------------------------------------------------------

# 18. Execution Response

``` json
{
  "results":[],
  "hasMore":true,
  "continuationToken":"...",
  "count":null,
  "executionInfo":{
    "provider":"Cosmos",
    "executionTimeMs":15,
    "requestCharge":4.1
  }
}
```

ExecutionInfo is optional.

------------------------------------------------------------------------

# 19. Non-Functional Requirements

-   Thread safe
-   Fully asynchronous
-   Provider independent
-   Extensible operator registry
-   Configurable limits
-   Secure continuation tokens
-   Comprehensive diagnostics
-   Unit-testable translators

------------------------------------------------------------------------

# 20. Future Enhancements

-   Named reusable filters
-   Computed projections
-   Parameterized query templates
-   Full-text search abstraction
-   Geospatial operators
-   Query plan caching
-   Query complexity analysis
-   Custom operators
-   Authorization-aware field filtering
