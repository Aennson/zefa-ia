using System.Net;
using Xunit;
using ZefaIA.Overlay;

namespace ZefaIA.Overlay.Tests;

/// <summary>
/// The "Testar chaves" button. Its value is telling a bad key apart from an unreachable
/// service — reporting a network outage as "chave recusada" would send the user hunting
/// for a new key they do not need.
/// </summary>
public class ApiKeyValidatorTests
{
    [Fact]
    public async Task AnAcceptedKeyIsReportedValid()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        using var validator = new ApiKeyValidator(new HttpClient(handler));

        var result = await validator.CheckAnthropicAsync("sk-ant-good");

        Assert.Equal(ApiKeyStatus.Valid, result.Status);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task ARefusedKeyIsReportedRejected(HttpStatusCode status)
    {
        var handler = new StubHandler(status);
        using var validator = new ApiKeyValidator(new HttpClient(handler));

        var result = await validator.CheckAnthropicAsync("sk-ant-revoked");

        Assert.Equal(ApiKeyStatus.Rejected, result.Status);
    }

    [Fact]
    public async Task ARateLimitedKeyCountsAsValid()
    {
        // 429 means the service authenticated the key and then throttled it. Calling that
        // "rejected" would be plainly wrong.
        var handler = new StubHandler(HttpStatusCode.TooManyRequests);
        using var validator = new ApiKeyValidator(new HttpClient(handler));

        var result = await validator.CheckAnthropicAsync("sk-ant-busy");

        Assert.Equal(ApiKeyStatus.Valid, result.Status);
    }

    [Fact]
    public async Task AServiceOutageIsNotBlamedOnTheKey()
    {
        var handler = new StubHandler(HttpStatusCode.InternalServerError);
        using var validator = new ApiKeyValidator(new HttpClient(handler));

        var result = await validator.CheckAnthropicAsync("sk-ant-fine");

        Assert.Equal(ApiKeyStatus.Unreachable, result.Status);
    }

    [Fact]
    public async Task NoNetworkIsNotBlamedOnTheKeyEither()
    {
        var handler = new StubHandler(new HttpRequestException("no such host"));
        using var validator = new ApiKeyValidator(new HttpClient(handler));

        var result = await validator.CheckAnthropicAsync("sk-ant-fine");

        Assert.Equal(ApiKeyStatus.Unreachable, result.Status);
    }

    [Fact]
    public async Task AnEmptyKeyIsReportedWithoutCallingTheService()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        using var validator = new ApiKeyValidator(new HttpClient(handler));

        var result = await validator.CheckAnthropicAsync("   ");

        Assert.Equal(ApiKeyStatus.Empty, result.Status);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task TheAnthropicProbeSendsTheHeadersThatServiceRequires()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        using var validator = new ApiKeyValidator(new HttpClient(handler));

        await validator.CheckAnthropicAsync("sk-ant-good");

        Assert.Equal(ApiKeyValidator.AnthropicProbeUrl, handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("sk-ant-good", handler.LastRequest.Headers.GetValues("x-api-key").Single());
        // Without anthropic-version the API answers 400, which would look like an outage.
        Assert.Equal("2023-06-01", handler.LastRequest.Headers.GetValues("anthropic-version").Single());
    }

    [Fact]
    public async Task TheElevenLabsProbeUsesItsOwnHeaderName()
    {
        // xi-api-key, not x-api-key. Sending the Anthropic header name would come back 401
        // and report a perfectly good key as rejected.
        var handler = new StubHandler(HttpStatusCode.OK);
        using var validator = new ApiKeyValidator(new HttpClient(handler));

        await validator.CheckElevenLabsAsync("sk_eleven_good");

        Assert.Equal(ApiKeyValidator.ElevenLabsProbeUrl, handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("sk_eleven_good", handler.LastRequest.Headers.GetValues("xi-api-key").Single());
        Assert.False(handler.LastRequest.Headers.Contains("x-api-key"));
    }

    [Fact]
    public async Task TheProbesOnlyRead()
    {
        // A probe that generated tokens would bill the user every time they pressed the
        // button. Both endpoints must stay GETs.
        var handler = new StubHandler(HttpStatusCode.OK);
        using var validator = new ApiKeyValidator(new HttpClient(handler));

        await validator.CheckAnthropicAsync("sk-ant-good");
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);

        await validator.CheckElevenLabsAsync("sk_eleven_good");
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode? _status;
        private readonly Exception? _failure;

        public int Calls { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }

        public StubHandler(HttpStatusCode status) => _status = status;
        public StubHandler(Exception failure) => _failure = failure;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastRequest = request;

            if (_failure != null)
                return Task.FromException<HttpResponseMessage>(_failure);

            return Task.FromResult(new HttpResponseMessage(_status!.Value));
        }
    }
}
