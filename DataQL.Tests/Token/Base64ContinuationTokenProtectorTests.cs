using DataQL.Token;

namespace DataQL.Tests.Token;

public class Base64ContinuationTokenProtectorTests
{
    [Fact]
    public void ProtectAndUnprotect_RoundTripsEnvelope()
    {
        var protector = new Base64ContinuationTokenProtector();
        var input = new ContinuationTokenEnvelope
        {
            Provider = "Cosmos",
            QueryShapeHash = "hash",
            ProviderToken = "provider-token"
        };

        var token = protector.Protect(input);
        var ok = protector.TryUnprotect(token, out var output);

        Assert.True(ok);
        Assert.NotNull(output);
        Assert.Equal(input.Provider, output!.Provider);
        Assert.Equal(input.QueryShapeHash, output.QueryShapeHash);
        Assert.Equal(input.ProviderToken, output.ProviderToken);
    }

    [Fact]
    public void TryUnprotect_WithInvalidToken_ReturnsFalse()
    {
        var protector = new Base64ContinuationTokenProtector();

        var ok = protector.TryUnprotect("not-base64", out var output);

        Assert.False(ok);
        Assert.Null(output);
    }
}
