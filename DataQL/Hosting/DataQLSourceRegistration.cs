using System;
using System.Threading.Tasks;

namespace DataQL;

public sealed record DataQLSourceRegistration(
    string Provider,
    Func<IServiceProvider, ValueTask<IDataQLSession>> SessionFactory);
