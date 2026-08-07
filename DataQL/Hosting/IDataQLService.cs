using System.Threading;
using System.Threading.Tasks;
using DataQL.Contracts;

namespace DataQL;

public interface IDataQLService
{
    Task<QueryResponse<T>> ExecuteAsync<T>(
        string sourceKey,
        string sourceName,
        QueryRequest request,
        CancellationToken cancellationToken = default);
}
