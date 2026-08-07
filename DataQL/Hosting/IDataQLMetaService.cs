using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataQL.Contracts;

namespace DataQL;

public interface IDataQLMetaService
{
    Task<IReadOnlyList<DataQLSourceInfo>> ListSourcesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DataQLTableInfo>> ListTablesAsync(
        string sourceKey,
        CancellationToken cancellationToken = default);

    Task<DataQLTableSchema> GetTableSchemaAsync(
        string sourceKey,
        string tableName,
        CancellationToken cancellationToken = default);
}
