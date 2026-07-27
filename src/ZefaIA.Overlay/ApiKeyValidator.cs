using System.Net;
using System.Net.Http;

namespace ZefaIA.Overlay;

public enum ApiKeyStatus
{
    /// <summary>No key to check.</summary>
    Empty,
    Valid,
    /// <summary>The service rejected the key — wrong, revoked, or lacking the needed scope.</summary>
    Rejected,
    /// <summary>Could not reach the service, so the key is neither proven good nor bad.</summary>
    Unreachable
}

public record ApiKeyCheck(ApiKeyStatus Status, string Message);

/// <summary>
/// Checks an API key against the service that issues it, so the Settings window can say
/// "this key works" instead of the user finding out mid-meeting.
///
/// Both checks hit read-only endpoints: listing models and reading the account. Nothing
/// is generated, so pressing the button repeatedly costs nothing.
/// </summary>
public sealed class ApiKeyValidator : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    internal const string AnthropicProbeUrl = "https://api.anthropic.com/v1/models?limit=1";
    internal const string ElevenLabsProbeUrl = "https://api.elevenlabs.io/v1/user";

    public ApiKeyValidator(HttpClient? httpClient = null)
    {
        _ownsClient = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public Task<ApiKeyCheck> CheckAnthropicAsync(string? apiKey, CancellationToken ct = default) =>
        CheckAsync(apiKey, AnthropicProbeUrl, request =>
        {
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
        }, ct);

    public Task<ApiKeyCheck> CheckElevenLabsAsync(string? apiKey, CancellationToken ct = default) =>
        CheckAsync(apiKey, ElevenLabsProbeUrl, request =>
        {
            request.Headers.Add("xi-api-key", apiKey);
        }, ct);

    private async Task<ApiKeyCheck> CheckAsync(
        string? apiKey, string url, Action<HttpRequestMessage> authorize, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return new ApiKeyCheck(ApiKeyStatus.Empty, "nenhuma chave informada");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            authorize(request);

            using var response = await _http.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
                return new ApiKeyCheck(ApiKeyStatus.Valid, "chave valida");

            return response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                    new ApiKeyCheck(ApiKeyStatus.Rejected, "chave recusada — confira se copiou inteira e se ainda esta ativa"),
                HttpStatusCode.TooManyRequests =>
                    // The key was accepted well enough to be rate limited.
                    new ApiKeyCheck(ApiKeyStatus.Valid, "chave valida (limite de requisicoes atingido)"),
                _ =>
                    new ApiKeyCheck(ApiKeyStatus.Unreachable, $"resposta inesperada do servico ({(int)response.StatusCode})")
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new ApiKeyCheck(ApiKeyStatus.Unreachable, "nao foi possivel contatar o servico — verifique a conexao");
        }
    }

    public void Dispose()
    {
        if (_ownsClient)
            _http.Dispose();
    }
}
