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

namespace DataQL.SqlServer.Metadata;

public sealed class SqlServerMetadataProvider
{
    public async Task<IReadOnlyList<DataQLTableInfo>> ListTablesAsync(
        IDbConnection connection,
        CancellationToken cancellationToken = default)
    {
        await using var command = CreateCommand(
            connection,
            """
            SELECT TABLE_SCHEMA, TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_SCHEMA, TABLE_NAME;
            """);

        var tables = new List<DataQLTableInfo>();
        await using var reader = await ExecuteReaderAsync(command, cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var schema = reader.GetString(0);
            var name = reader.GetString(1);
            tables.Add(new DataQLTableInfo(
                Name: $"{schema}.{name}",
                Schema: schema));
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
        var qualified = $"{schema}.{table}";

        await using var command = CreateCommand(
            connection,
            """
            SELECT
                COLUMN_NAME,
                DATA_TYPE,
                IS_NULLABLE,
                CHARACTER_MAXIMUM_LENGTH,
                NUMERIC_PRECISION,
                NUMERIC_SCALE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = @schema
              AND TABLE_NAME = @table
            ORDER BY ORDINAL_POSITION;
            """);
        AddParameter(command, "@schema", schema);
        AddParameter(command, "@table", table);

        await using var reader = await ExecuteReaderAsync(command, cancellationToken);

        var properties = new JsonObject();
        var required = new JsonArray();

        while (await reader.ReadAsync(cancellationToken))
        {
            var columnName = reader.GetString(0);
            var dbType = reader.GetString(1);
            var isNullable = string.Equals(reader.GetString(2), "YES", StringComparison.OrdinalIgnoreCase);

            var property = MapColumnToJsonSchema(dbType);
            property["x-dataql-dbType"] = dbType;

            if (!reader.IsDBNull(3))
            {
                property["x-dataql-maxLength"] = reader.GetInt32(3);
            }

            if (!reader.IsDBNull(4))
            {
                property["x-dataql-precision"] = Convert.ToInt32(reader.GetValue(4));
            }

            if (!reader.IsDBNull(5))
            {
                property["x-dataql-scale"] = Convert.ToInt32(reader.GetValue(5));
            }

            properties[columnName] = property;
            if (!isNullable)
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
            Provider = ProviderName.SqlServer,
            Schema = JsonSerializer.SerializeToElement(schemaNode)
        };
    }

    private static JsonObject MapColumnToJsonSchema(string dbType)
    {
        var normalized = dbType.Trim().ToLowerInvariant();
        var jsonType = normalized switch
        {
            "bit" => "boolean",
            "tinyint" or "smallint" or "int" or "bigint" => "integer",
            "decimal" or "numeric" or "money" or "smallmoney"
                or "float" or "real" => "number",
            "date" or "datetime" or "datetime2" or "smalldatetime"
                or "datetimeoffset" or "time" => "string",
            "uniqueidentifier" => "string",
            "xml" or "json" => "string",
            _ => "string"
        };

        var property = new JsonObject { ["type"] = jsonType };
        if (normalized is "datetime" or "datetime2" or "smalldatetime" or "datetimeoffset")
        {
            property["format"] = "date-time";
        }
        else if (normalized is "date")
        {
            property["format"] = "date";
        }
        else if (normalized is "time")
        {
            property["format"] = "time";
        }
        else if (normalized is "uniqueidentifier")
        {
            property["format"] = "uuid";
        }

        return property;
    }

    private static (string Schema, string Table) SplitTableName(string tableName)
    {
        var parts = tableName.Split('.', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2)
        {
            return (parts[0], parts[1]);
        }

        return ("dbo", tableName.Trim());
    }

    private static DbCommand CreateCommand(IDbConnection connection, string sql)
    {
        if (connection is not DbConnection dbConnection)
        {
            throw new NotSupportedException("SqlServer metadata requires a DbConnection.");
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
