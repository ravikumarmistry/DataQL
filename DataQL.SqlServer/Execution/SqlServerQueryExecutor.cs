using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using DataQL.SqlServer.Translation;
using Microsoft.Extensions.Logging;

namespace DataQL.SqlServer.Execution;

public sealed class SqlServerQueryExecutor(
    ILogger<SqlServerQueryExecutor>? logger = null) : ISqlServerQueryExecutor
{
    private readonly ILogger<SqlServerQueryExecutor>? _logger = logger;

    public async Task<IReadOnlyList<T>> ExecuteRowsAsync<T>(
        IDbConnection connection,
        SqlServerSqlTranslationResult translation,
        CancellationToken cancellationToken = default)
    {
        LogQuery("rows", translation.Sql, translation.Parameters);

        var command = new CommandDefinition(
            commandText: translation.Sql,
            parameters: ToDynamicParameters(translation.Parameters),
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<T>(command);
        return rows.ToList();
    }

    public async Task<long> ExecuteCountAsync(
        IDbConnection connection,
        SqlServerSqlTranslationResult translation,
        CancellationToken cancellationToken = default)
    {
        var wrappedCountSql = "SELECT COUNT(1) FROM (" + translation.Sql + ") AS [c]";
        LogQuery("count", wrappedCountSql, translation.Parameters);

        var command = new CommandDefinition(
            commandText: wrappedCountSql,
            parameters: ToDynamicParameters(translation.Parameters),
            cancellationToken: cancellationToken);

        return await connection.ExecuteScalarAsync<long>(command);
    }

    private static DynamicParameters ToDynamicParameters(IReadOnlyDictionary<string, object?> parameters)
    {
        var dapperParams = new DynamicParameters();
        foreach (var pair in parameters)
        {
            dapperParams.Add(pair.Key, pair.Value);
        }

        return dapperParams;
    }

    private void LogQuery(string queryType, string sql, IReadOnlyDictionary<string, object?> parameters)
    {
        if (_logger is null)
        {
            return;
        }

        _logger.LogInformation(
            "DataQL SQL Server {QueryType} query: {Sql} | Parameters: {Parameters}",
            queryType,
            sql,
            FormatParameters(parameters));
    }

    private static string FormatParameters(IReadOnlyDictionary<string, object?> parameters)
    {
        if (parameters.Count == 0)
        {
            return "{}";
        }

        var parts = parameters
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(pair => pair.Key + "=" + (pair.Value is null ? "null" : pair.Value));

        return "{" + string.Join(", ", parts) + "}";
    }
}
