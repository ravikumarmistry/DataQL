using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace DataQL;

public sealed class AdoDataQLSession : IDataQLSession
{
    private readonly bool _ownsConnection;
    private bool _disposed;

    private AdoDataQLSession(string provider, IDbConnection connection, bool ownsConnection)
    {
        Provider = provider;
        Connection = connection;
        _ownsConnection = ownsConnection;
    }

    public string Provider { get; }

    public IDbConnection Connection { get; }

    public static async ValueTask<AdoDataQLSession> CreateAsync(
        string provider,
        IDbConnection connection,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new ArgumentException("Provider is required.", nameof(provider));
        }

        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        await DataQLConnectionHelper.OpenAsync(connection, cancellationToken);
        return new AdoDataQLSession(provider, connection, ownsConnection: true);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        if (_ownsConnection)
        {
            Connection.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
