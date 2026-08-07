using System;
using DataQL.AspNetCore.OpenApi;
using DataQL.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DataQL.AspNetCore;

public static class DataQLEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapDataQL(
        this IEndpointRouteBuilder endpoints,
        string prefix = "/dataql")
    {
        if (endpoints is null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }

        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("Prefix is required.", nameof(prefix));
        }

        var normalizedPrefix = prefix.StartsWith('/')
            ? prefix
            : $"/{prefix}";
        normalizedPrefix = normalizedPrefix.TrimEnd('/');

        var metaPrefix = $"{normalizedPrefix}/meta";
        var queryPrefix = $"{normalizedPrefix}/query";

        endpoints.MapGet($"{metaPrefix}/openapi.json", async (
            IDataQLOpenApiDocumentProvider openApi,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var refresh = httpContext.Request.Query.ContainsKey("refresh");
            var document = await openApi.GetDocumentAsync(normalizedPrefix, refresh, cancellationToken);
            return Results.Json(document, contentType: "application/json");
        });

        endpoints.MapGet($"{metaPrefix}/sources", async (
            IDataQLMetaService metaService,
            CancellationToken cancellationToken) =>
        {
            var sources = await metaService.ListSourcesAsync(cancellationToken);
            return Results.Ok(sources);
        });

        endpoints.MapGet($"{metaPrefix}/sources/{{sourceKey}}/tables", async (
            string sourceKey,
            IDataQLMetaService metaService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(sourceKey))
            {
                return Results.BadRequest(new { Error = "Route parameter 'sourceKey' is required." });
            }

            var tables = await metaService.ListTablesAsync(sourceKey, cancellationToken);
            return Results.Ok(tables);
        });

        endpoints.MapGet($"{metaPrefix}/sources/{{sourceKey}}/tables/{{table}}/schema", async (
            string sourceKey,
            string table,
            IDataQLMetaService metaService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(sourceKey))
            {
                return Results.BadRequest(new { Error = "Route parameter 'sourceKey' is required." });
            }

            if (string.IsNullOrWhiteSpace(table))
            {
                return Results.BadRequest(new { Error = "Route parameter 'table' is required." });
            }

            var schema = await metaService.GetTableSchemaAsync(sourceKey, table, cancellationToken);
            return Results.Ok(schema);
        });

        endpoints.MapPost($"{queryPrefix}/{{sourceKey}}/{{table}}", async (
            string sourceKey,
            string table,
            QueryRequest request,
            IDataQLService dataQLService,
            CancellationToken cancellationToken) =>
        {
            return await DataQLHttpResults.ExecuteAsync(async () =>
            {
                if (string.IsNullOrWhiteSpace(sourceKey))
                {
                    return Results.BadRequest(new { Error = "Route parameter 'sourceKey' is required." });
                }

                if (string.IsNullOrWhiteSpace(table))
                {
                    return Results.BadRequest(new { Error = "Route parameter 'table' is required." });
                }

                var response = await dataQLService.ExecuteAsync<object>(
                    sourceKey,
                    table,
                    request,
                    cancellationToken);

                return Results.Ok(response);
            });
        });

        return endpoints;
    }

    public static IEndpointRouteBuilder MapDataQLEndpoint(
        this IEndpointRouteBuilder endpoints,
        string route,
        string sourceKey)
    {
        if (endpoints is null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }

        if (string.IsNullOrWhiteSpace(route))
        {
            throw new ArgumentException("Route is required.", nameof(route));
        }

        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            throw new ArgumentException("Source key is required.", nameof(sourceKey));
        }

        if (!route.Contains("{table}", StringComparison.Ordinal))
        {
            throw new ArgumentException("Route must include a required '{table}' parameter for provider-resolved source name.", nameof(route));
        }

        endpoints.MapPost(route, async (
            string table,
            QueryRequest request,
            IDataQLService dataQLService,
            CancellationToken cancellationToken) =>
        {
            return await DataQLHttpResults.ExecuteAsync(async () =>
            {
                if (string.IsNullOrWhiteSpace(table))
                {
                    return Results.BadRequest(new { Error = "Route parameter 'table' is required." });
                }

                var response = await dataQLService.ExecuteAsync<object>(
                    sourceKey,
                    table,
                    request,
                    cancellationToken);

                return Results.Ok(response);
            });
        });

        return endpoints;
    }
}
