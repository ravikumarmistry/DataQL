# Generic Query DSL Specification v1.1

> **Change from v1.0:** Every operator now includes at least one example
> request.

## Operator Examples

Assume the following document:

``` json
{
  "id":"1001",
  "name":"John",
  "age":30,
  "active":true,
  "city":"Delhi",
  "address":{"country":"India","city":"Delhi"},
  "skills":["Azure",".NET","C#"],
  "projects":[
    {"name":"Portal","status":"Completed","hours":120},
    {"name":"Mobile","status":"Active","hours":40}
  ]
}
```

### Equality (\$eq)

``` json
{
  "where":{
    "city":"Delhi"
  }
}
```

### Not Equal (\$ne)

``` json
{
  "where":{
    "city":{"$ne":"Delhi"}
  }
}
```

### Greater Than (\$gt)

``` json
{
  "where":{
    "age":{"$gt":18}
  }
}
```

### Greater Than Or Equal (\$gte)

``` json
{
  "where":{
    "age":{"$gte":18}
  }
}
```

### Less Than (\$lt)

``` json
{
  "where":{
    "age":{"$lt":60}
  }
}
```

### Less Than Or Equal (\$lte)

``` json
{
  "where":{
    "age":{"$lte":60}
  }
}
```

### In (\$in)

``` json
{
  "where":{
    "city":{"$in":["Delhi","Mumbai"]}
  }
}
```

### Not In (\$nin)

``` json
{
  "where":{
    "city":{"$nin":["Delhi","Mumbai"]}
  }
}
```

### Contains (string)

``` json
{
  "where":{
    "name":{"$contains":"oh"}
  }
}
```

### Starts With

``` json
{
  "where":{
    "name":{"$startsWith":"Jo"}
  }
}
```

### Ends With

``` json
{
  "where":{
    "name":{"$endsWith":"hn"}
  }
}
```

### Regex

``` json
{
  "where":{
    "name":{"$regex":"^Jo.*"}
  }
}
```

### Exists

``` json
{
  "where":{
    "address.city":{"$exists":true}
  }
}
```

### Is Null

``` json
{
  "where":{
    "deletedAt":{"$isNull":true}
  }
}
```

### AND

``` json
{
  "where":{
    "$and":[
      {"city":"Delhi"},
      {"active":true}
    ]
  }
}
```

### OR

``` json
{
  "where":{
    "$or":[
      {"city":"Delhi"},
      {"city":"Mumbai"}
    ]
  }
}
```

### NOT

``` json
{
  "where":{
    "$not":{
      "active":true
    }
  }
}
```

### Primitive Array Contains

``` json
{
  "where":{
    "skills":{"$contains":"Azure"}
  }
}
```

### Primitive Array Contains Any

``` json
{
  "where":{
    "skills":{"$containsAny":["Azure","Java"]}
  }
}
```

### Primitive Array Contains All

``` json
{
  "where":{
    "skills":{"$containsAll":["Azure",".NET"]}
  }
}
```

### Array Size

``` json
{
  "where":{
    "skills":{"$size":3}
  }
}
```

### Array Is Empty

``` json
{
  "where":{
    "skills":{"$isEmpty":true}
  }
}
```

### Array Of Objects (\$any)

``` json
{
  "where":{
    "projects":{
      "$any":{
        "status":"Active",
        "hours":{"$gt":20}
      }
    }
  }
}
```

### Nested Object

``` json
{
  "where":{
    "address.country":"India"
  }
}
```

## Group Aggregation Examples

### Group By Department And City

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

### Filter Then Group

``` json
{
  "where":{
    "active":true
  },
  "group":{
    "groupBy":[
      "department"
    ],
    "metrics":[
      {
        "field":"*",
        "operation":"count",
        "alias":"employees"
      }
    ]
  }
}
```

### Sort Grouped Results

``` json
{
  "group":{
    "groupBy":[
      "department"
    ],
    "metrics":[
      {
        "field":"*",
        "operation":"count",
        "alias":"employees"
      }
    ]
  },
  "order":[
    {
      "field":"employees",
      "direction":"desc"
    }
  ]
}
```

### Limit Grouped Results

``` json
{
  "group":{
    "groupBy":[
      "department"
    ],
    "metrics":[
      {
        "field":"*",
        "operation":"count",
        "alias":"employees"
      }
    ]
  },
  "limit":10
}
```

Supported metric operations:

- count
- sum
- avg
- min
- max
- first
- last

## Recommendation

Every operator section in the full specification should follow this
template:

1.  Description
2.  Supported value types
3.  JSON request example
4.  Expected semantic behavior
5.  Provider translations (EF Core, SQL Server, Cosmos DB, MongoDB)
6.  Validation rules
7.  Performance considerations
8.  Unsupported provider behavior

For grouping, follow the same structure for each metric operation and include:

1.  Description
2.  Valid `field` values (for example `*` for count)
3.  JSON request example
4.  SQL equivalence
5.  Provider translations (EF Core, SQL Server, Cosmos DB, MongoDB)
6.  Validation rules
7.  Performance considerations
8.  Unsupported provider behavior
