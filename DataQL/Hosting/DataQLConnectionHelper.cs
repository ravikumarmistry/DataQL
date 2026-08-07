using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace DataQL;

internal static class DataQLConnectionHelper
{
    public static Task OpenAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        if (connection is DbConnection dbConnection)
        {
            return dbConnection.OpenAsync(cancellationToken);
        }

        connection.Open();
        return Task.CompletedTask;
    }
}
