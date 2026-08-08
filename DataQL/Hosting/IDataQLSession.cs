using System;
using System.Threading.Tasks;

namespace DataQL;

public interface IDataQLSession : IAsyncDisposable
{
    string Provider { get; }
}
