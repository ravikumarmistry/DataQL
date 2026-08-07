using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using DataQL.Abstractions;
using DataQL.Contracts;

namespace DataQL.Sqlite.Metadata;

public sealed class SqliteMetadataProvider
{
    public async Task<IReadOnlyList<DataQLTableInfo>> ListTablesAsync(
        IDbConnection connection,
        CancellationToken cancellationToken = default)
    {
        await using var command = CreateCommand(
            connection,
            """
            SELECT name
            FROM sqlite_master
            WHERE type = 'table'
              AND name NOT LIKE 'sqlite_%'
            ORDER BY name;
            """);

        var tables = new List<DataQLTableInfo>();
        await using var reader = await ExecuteReaderAsync(command, cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var name = reader.GetString(0);
            tables.Add(new DataQLTableInfo(Name: name, Schema: "main"));
        }

        return tables;
    }

    public async Task<DataQLTableSchema> GetTableSchemaAsync(
        IDbConnection connection,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        var (schema, table) = SplitTableName(tableName);
        var qualified = schema is null ? table : $"{schema}.{table}";

        if (!await TableExistsAsync(connection, schema, table, cancellationToken))
        {
            throw new InvalidOperationException($"Table '{qualified}' was not found.");
        }

        var pragmaSql = schema is null
            ? $"PRAGMA table_info({QuoteIdent(table)});"
            : $"PRAGMA {QuoteIdent(schema)}.table_info({QuoteIdent(table)});";

        await using var command = CreateCommand(connection, pragmaSql);
        await using var reader = await ExecuteReaderAsync(command, cancellationToken);

        var properties = new JsonObject();
        var required = new JsonArray();

        while (await reader.ReadAsync(cancellationToken))
        {
            var columnName = reader.GetString(1);
            var dbType = reader.IsDBNull(2) ? "TEXT" : reader.GetString(2);
            var notNull = reader.GetInt64(3) != 0;
            var isPk = reader.GetInt64(5) != 0;

            var property = MapColumnToJsonSchema(dbType);
            property["x-dataql-dbType"] = dbType;
            if (isPk)
            {
                property["x-dataql-primaryKey"] = true;
            }

            properties[columnName] = property;

            if (notNull || isPk)
            {
                required.Add(columnName);
            }
        }

        if (properties.Count == 0)
        {
            throw new InvalidOperationException($"Table '{qualified}' was not found.");
        }

        var schemaNode = new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["type"] = "object",
            ["properties"] = properties
        };

        if (required.Count > 0)
        {
            schemaNode["required"] = required;
        }

        return new DataQLTableSchema
        {
            Table = qualified,
            Provider = ProviderName.Sqlite,
            Schema = JsonSerializer.SerializeToElement(schemaNode)
        };
    }

    private static JsonObject MapColumnToJsonSchema(string dbType)
    {
        var normalized = dbType.Trim().ToUpperInvariant();
        var jsonType = normalized switch
        {
            var t when t.Contains("INT", StringComparison.Ordinal) => "integer",
            var t when t.Contains("BOOL", StringComparison.Ordinal) => "boolean",
            var t when t.Contains("REAL", StringComparison.Ordinal)
                || t.Contains("FLOA", StringComparison.Ordinal)
                || t.Contains("DOUB", StringComparison.Ordinal)
                || t.Contains("NUM", StringComparison.Ordinal)
                || t.Contains("DEC", StringComparison.Ordinal) => "number",
            var t when t.Contains("BLOB", StringComparison.Ordinal) => "string",
            _ => "string"
        };

        return new JsonObject { ["type"] = jsonType };
    }

    private static async Task<bool> TableExistsAsync(
        IDbConnection connection,
        string? schema,
        string table,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            """
            SELECT 1
            FROM sqlite_master
            WHERE type = 'table'
              AND name = $name
            LIMIT 1;
            """);
        AddParameter(command, "$name", table);

        await using var reader = await ExecuteReaderAsync(command, cancellationToken);
        var exists = await reader.ReadAsync(cancellationToken);
        _ = schema; // SQLite table names in sqlite_master are unqualified; schema used for PRAGMA only.
        return exists;
    }

    private static (string? Schema, string Table) SplitTableName(string tableName)
    {
        var parts = tableName.Split('.', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2)
        {
            return (parts[0], parts[1]);
        }

        return (null, tableName.Trim());
    }

    private static string QuoteIdent(string ident)
    {
        return "\"" + ident.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static DbCommand CreateCommand(IDbConnection connection, string sql)
    {
        if (connection is not DbConnection dbConnection)
        {
            throw new NotSupportedException("Sqlite metadata requires a DbConnection.");
        }

        var command = dbConnection.CreateCommand();
        command.CommandText = sql;
        return command;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static Task<DbDataReader> ExecuteReaderAsync(DbCommand command, CancellationToken cancellationToken)
    {
        return command.ExecuteReaderAsync(cancellationToken);
    }
}
