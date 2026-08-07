using System;
using System.Text;
using System.Text.Json;

namespace DataQL.Token;

public sealed class Base64ContinuationTokenProtector : IContinuationTokenProtector
{
    public string Protect(ContinuationTokenEnvelope envelope)
    {
        var json = JsonSerializer.Serialize(envelope);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    public bool TryUnprotect(string token, out ContinuationTokenEnvelope? envelope)
    {
        envelope = null;

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            envelope = JsonSerializer.Deserialize<ContinuationTokenEnvelope>(json);
            return envelope is not null;
        }
        catch
        {
            return false;
        }
    }
}
