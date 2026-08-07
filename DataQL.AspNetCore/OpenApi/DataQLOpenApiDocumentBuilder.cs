using System.Text.Json;
using System.Text.Json.Nodes;
using DataQL.Contracts;
using Microsoft.Extensions.Logging;

namespace DataQL.AspNetCore.OpenApi;

public sealed class DataQLOpenApiDocumentBuilder
{
    private readonly IDataQLMetaService _metaService;
    private readonly ILogger<DataQLOpenApiDocumentBuilder> _logger;

    public DataQLOpenApiDocumentBuilder(
        IDataQLMetaService metaService,
        ILogger<DataQLOpenApiDocumentBuilder> logger)
    {
        _metaService = metaService ?? throw new ArgumentNullException(nameof(metaService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<JsonObject> BuildAsync(
        string prefix,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("Prefix is required.", nameof(prefix));
        }

        var normalizedPrefix = prefix.StartsWith('/')
            ? prefix.TrimEnd('/')
            : "/" + prefix.TrimEnd('/');

        var schemas = new JsonObject
        {
            ["QueryRequest"] = BuildQueryRequestSchema(),
            ["QueryResponse"] = BuildQueryResponseSchema(itemsSchema: new JsonObject { ["type"] = "object" }),
            ["DataQLSourceInfo"] = BuildSourceInfoSchema(),
            ["DataQLTableInfo"] = BuildTableInfoSchema(),
            ["DataQLTableSchema"] = BuildTableSchemaEnvelopeSchema()
        };

        var metaPrefix = $"{normalizedPrefix}/meta";
        var queryPrefix = $"{normalizedPrefix}/query";

        var paths = new JsonObject
        {
            [$"{metaPrefix}/openapi.json"] = BuildOpenApiSelfPath(),
            [$"{metaPrefix}/sources"] = BuildListSourcesPath(),
            [$"{metaPrefix}/sources/{{sourceKey}}/tables"] = BuildListTablesPath(),
            [$"{metaPrefix}/sources/{{sourceKey}}/tables/{{table}}/schema"] = BuildGetTableSchemaPath(),
            [$"{queryPrefix}/{{sourceKey}}/{{table}}"] = BuildGenericQueryPath()
        };

        var sources = await _metaService.ListSourcesAsync(cancellationToken);
        foreach (var source in sources)
        {
            IReadOnlyList<DataQLTableInfo> tables;
            try
            {
                tables = await _metaService.ListTablesAsync(source.Key, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to list tables for DataQL source '{SourceKey}'.", source.Key);
                continue;
            }

            foreach (var table in tables)
            {
                try
                {
                    var tableSchema = await _metaService.GetTableSchemaAsync(
                        source.Key,
                        table.Name,
                        cancellationToken);

                    var schemaKey = BuildSchemaKey(source.Key, table.Name);
                    var componentSchema = CloneAsObject(tableSchema.Schema) ?? new JsonObject { ["type"] = "object" };
                    componentSchema["title"] = $"{source.Key}.{table.Name}";
                    componentSchema["x-dataql-sourceKey"] = source.Key;
                    componentSchema["x-dataql-table"] = table.Name;
                    componentSchema["x-dataql-provider"] = tableSchema.Provider;
                    schemas[schemaKey] = componentSchema;

                    var concretePath = $"{queryPrefix}/{EncodePathSegment(source.Key)}/{EncodePathSegment(table.Name)}";
                    paths[concretePath] = BuildTableQueryPath(source.Key, table.Name, schemaKey);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to load schema for DataQL source '{SourceKey}' table '{Table}'.",
                        source.Key,
                        table.Name);
                }
            }
        }

        return new JsonObject
        {
            ["openapi"] = "3.0.3",
            ["info"] = new JsonObject
            {
                ["title"] = "DataQL",
                ["version"] = "1.0.0",
                ["description"] = "Runtime OpenAPI document generated from registered DataQL sources and table schemas."
            },
            ["paths"] = paths,
            ["components"] = new JsonObject
            {
                ["schemas"] = schemas
            }
        };
    }

    private static string BuildSchemaKey(string sourceKey, string tableName)
    {
        static string Sanitize(string value)
        {
            var chars = value.Select(ch =>
                char.IsLetterOrDigit(ch) || ch is '_' or '-'
                    ? ch
                    : '_').ToArray();
            return new string(chars);
        }

        return $"{Sanitize(sourceKey)}__{Sanitize(tableName)}";
    }

    private static string EncodePathSegment(string value)
    {
        // Keep dots (schema.table); escape characters that break path templates.
        return Uri.EscapeDataString(value).Replace("%2E", ".", StringComparison.Ordinal);
    }

    private static JsonObject? CloneAsObject(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        return JsonNode.Parse(element.GetRawText()) as JsonObject;
    }

    private static JsonObject BuildQueryRequestSchema()
    {
        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["where"] = new JsonObject { ["type"] = "object", ["additionalProperties"] = true },
                ["order"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["field"] = new JsonObject { ["type"] = "string" },
                            ["direction"] = new JsonObject
                            {
                                ["type"] = "string",
                                ["enum"] = new JsonArray("asc", "desc")
                            }
                        }
                    }
                },
                ["select"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "string" }
                },
                ["exclude"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "string" }
                },
                ["distinct"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "string" }
                },
                ["limit"] = new JsonObject { ["type"] = "integer" },
                ["continuationToken"] = new JsonObject { ["type"] = "string", ["nullable"] = true },
                ["includeCount"] = new JsonObject { ["type"] = "boolean" },
                ["group"] = new JsonObject
                {
                    ["type"] = "object",
                    ["nullable"] = true,
                    ["properties"] = new JsonObject
                    {
                        ["groupBy"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["items"] = new JsonObject { ["type"] = "string" }
                        },
                        ["metrics"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["items"] = new JsonObject
                            {
                                ["type"] = "object",
                                ["properties"] = new JsonObject
                                {
                                    ["field"] = new JsonObject { ["type"] = "string" },
                                    ["operation"] = new JsonObject { ["type"] = "string" },
                                    ["alias"] = new JsonObject { ["type"] = "string" }
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    private static JsonObject BuildQueryResponseSchema(JsonObject itemsSchema)
    {
        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["results"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = itemsSchema
                },
                ["hasMore"] = new JsonObject { ["type"] = "boolean" },
                ["continuationToken"] = new JsonObject { ["type"] = "string", ["nullable"] = true },
                ["count"] = new JsonObject { ["type"] = "integer", ["nullable"] = true }
            }
        };
    }

    private static JsonObject BuildSourceInfoSchema()
    {
        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["key"] = new JsonObject { ["type"] = "string" },
                ["provider"] = new JsonObject { ["type"] = "string" }
            }
        };
    }

    private static JsonObject BuildTableInfoSchema()
    {
        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["name"] = new JsonObject { ["type"] = "string" },
                ["schema"] = new JsonObject { ["type"] = "string", ["nullable"] = true }
            }
        };
    }

    private static JsonObject BuildTableSchemaEnvelopeSchema()
    {
        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["sourceKey"] = new JsonObject { ["type"] = "string" },
                ["table"] = new JsonObject { ["type"] = "string" },
                ["provider"] = new JsonObject { ["type"] = "string" },
                ["schema"] = new JsonObject { ["type"] = "object", ["additionalProperties"] = true }
            }
        };
    }

    private static JsonObject BuildOpenApiSelfPath()
    {
        return new JsonObject
        {
            ["get"] = new JsonObject
            {
                ["operationId"] = "getDataQLOpenApiDocument",
                ["summary"] = "Get the runtime DataQL OpenAPI document",
                ["parameters"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "refresh",
                        ["in"] = "query",
                        ["required"] = false,
                        ["schema"] = new JsonObject { ["type"] = "boolean" },
                        ["description"] = "When present, rebuilds the cached OpenAPI document."
                    }
                },
                ["responses"] = new JsonObject
                {
                    ["200"] = new JsonObject
                    {
                        ["description"] = "OpenAPI 3.0 document",
                        ["content"] = new JsonObject
                        {
                            ["application/json"] = new JsonObject
                            {
                                ["schema"] = new JsonObject { ["type"] = "object", ["additionalProperties"] = true }
                            }
                        }
                    }
                }
            }
        };
    }

    private static JsonObject BuildListSourcesPath()
    {
        return new JsonObject
        {
            ["get"] = new JsonObject
            {
                ["operationId"] = "listDataQLSources",
                ["summary"] = "List registered DataQL sources",
                ["responses"] = JsonOkArrayResponse("#/components/schemas/DataQLSourceInfo")
            }
        };
    }

    private static JsonObject BuildListTablesPath()
    {
        return new JsonObject
        {
            ["get"] = new JsonObject
            {
                ["operationId"] = "listDataQLTables",
                ["summary"] = "List tables for a DataQL source",
                ["parameters"] = new JsonArray { PathParameter("sourceKey") },
                ["responses"] = JsonOkArrayResponse("#/components/schemas/DataQLTableInfo")
            }
        };
    }

    private static JsonObject BuildGetTableSchemaPath()
    {
        return new JsonObject
        {
            ["get"] = new JsonObject
            {
                ["operationId"] = "getDataQLTableSchema",
                ["summary"] = "Get JSON Schema for a table",
                ["parameters"] = new JsonArray
                {
                    PathParameter("sourceKey"),
                    PathParameter("table")
                },
                ["responses"] = JsonOkObjectResponse("#/components/schemas/DataQLTableSchema")
            }
        };
    }

    private static JsonObject BuildGenericQueryPath()
    {
        return new JsonObject
        {
            ["post"] = new JsonObject
            {
                ["operationId"] = "executeDataQLQuery",
                ["summary"] = "Execute a DataQL query against any registered source/table",
                ["parameters"] = new JsonArray
                {
                    PathParameter("sourceKey"),
                    PathParameter("table")
                },
                ["requestBody"] = new JsonObject
                {
                    ["required"] = true,
                    ["content"] = new JsonObject
                    {
                        ["application/json"] = new JsonObject
                        {
                            ["schema"] = new JsonObject
                            {
                                ["$ref"] = "#/components/schemas/QueryRequest"
                            }
                        }
                    }
                },
                ["responses"] = JsonOkObjectResponse("#/components/schemas/QueryResponse")
            }
        };
    }

    private static JsonObject BuildTableQueryPath(string sourceKey, string tableName, string schemaKey)
    {
        var responseSchema = BuildQueryResponseSchema(new JsonObject
        {
            ["$ref"] = $"#/components/schemas/{schemaKey}"
        });

        return new JsonObject
        {
            ["post"] = new JsonObject
            {
                ["operationId"] = $"executeDataQLQuery_{schemaKey}",
                ["summary"] = $"Query {sourceKey}/{tableName}",
                ["tags"] = new JsonArray(sourceKey),
                ["x-dataql-sourceKey"] = sourceKey,
                ["x-dataql-table"] = tableName,
                ["requestBody"] = new JsonObject
                {
                    ["required"] = true,
                    ["content"] = new JsonObject
                    {
                        ["application/json"] = new JsonObject
                        {
                            ["schema"] = new JsonObject
                            {
                                ["$ref"] = "#/components/schemas/QueryRequest"
                            }
                        }
                    }
                },
                ["responses"] = new JsonObject
                {
                    ["200"] = new JsonObject
                    {
                        ["description"] = "OK",
                        ["content"] = new JsonObject
                        {
                            ["application/json"] = new JsonObject
                            {
                                ["schema"] = responseSchema
                            }
                        }
                    }
                }
            }
        };
    }

    private static JsonObject PathParameter(string name)
    {
        return new JsonObject
        {
            ["name"] = name,
            ["in"] = "path",
            ["required"] = true,
            ["schema"] = new JsonObject { ["type"] = "string" }
        };
    }

    private static JsonObject JsonOkArrayResponse(string itemRef)
    {
        return new JsonObject
        {
            ["200"] = new JsonObject
            {
                ["description"] = "OK",
                ["content"] = new JsonObject
                {
                    ["application/json"] = new JsonObject
                    {
                        ["schema"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["items"] = new JsonObject { ["$ref"] = itemRef }
                        }
                    }
                }
            }
        };
    }

    private static JsonObject JsonOkObjectResponse(string schemaRef)
    {
        return new JsonObject
        {
            ["200"] = new JsonObject
            {
                ["description"] = "OK",
                ["content"] = new JsonObject
                {
                    ["application/json"] = new JsonObject
                    {
                        ["schema"] = new JsonObject { ["$ref"] = schemaRef }
                    }
                }
            }
        };
    }
}
