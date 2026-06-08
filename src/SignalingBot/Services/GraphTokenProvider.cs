using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SignalingBot.Configuration;

namespace SignalingBot.Services;

/// <summary>Acquires and caches an app-only Graph token (client credentials).</summary>
public interface IGraphTokenProvider
{
    Task<string> GetTokenAsync(CancellationToken ct = default);

    /// <summary>Pre-fetch the token so the first call:answer isn't slowed by it
    /// (the policy answer window is only ~5 seconds).</summary>
    Task WarmUpAsync(CancellationToken ct = default);
}

public sealed class GraphTokenProvider : IGraphTokenProvider
{
    private const string Scope = "https://graph.microsoft.com/.default";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly BotOptions _options;
    private readonly ILogger<GraphTokenProvider> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _cachedToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public GraphTokenProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<BotOptions> options,
        ILogger<GraphTokenProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public Task WarmUpAsync(CancellationToken ct = default) => GetTokenAsync(ct);

    public async Task<string> GetTokenAsync(CancellationToken ct = default)
    {
        // Refresh a minute before expiry to stay clear of the answer deadline.
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _expiresAt.AddMinutes(-1))
        {
            return _cachedToken;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _expiresAt.AddMinutes(-1))
            {
                return _cachedToken;
            }

            var client = _httpClientFactory.CreateClient();
            var tokenEndpoint = $"https://login.microsoftonline.com/{_options.TenantId}/oauth2/v2.0/token";

            using var response = await client.PostAsync(
                tokenEndpoint,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = _options.ClientId,
                    ["client_secret"] = _options.ClientSecret,
                    ["scope"] = Scope,
                    ["grant_type"] = "client_credentials",
                }),
                ct);

            response.EnsureSuccessStatusCode();
            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct)
                        ?? throw new InvalidOperationException("Empty token response from Entra.");

            _cachedToken = token.AccessToken;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn);
            _logger.LogInformation("Acquired Graph app token; expires at {ExpiresAt:o}.", _expiresAt);
            return _cachedToken!;
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed record TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }
}
