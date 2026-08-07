using System;
using System.Data;

namespace DataQL;

public sealed record DataQLSourceRegistration(
    string Provider,
    Func<IServiceProvider, IDbConnection> ConnectionFactory);
