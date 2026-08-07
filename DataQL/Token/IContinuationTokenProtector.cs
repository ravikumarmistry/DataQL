namespace DataQL.Token;

public interface IContinuationTokenProtector
{
    string Protect(ContinuationTokenEnvelope envelope);
    bool TryUnprotect(string token, out ContinuationTokenEnvelope? envelope);
}
