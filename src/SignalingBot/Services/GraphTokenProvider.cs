using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SignalingBot.Configuration;

namespace SignalingBot.Services;

/// <summary>Acquires and caches app-only Graph tokens (client credentials), per tenant.</summary>
public interface IGraphTokenProvider
{
    /// <summary>
    /// Gets an app token for <paramref name="tenantId"/> (the tenant of the
    /// incoming call). When null/empty, falls back to the configured home tenant.
    /// A multi-tenant recorder must answer each call with a token from the calling
    /// tenant, so tokens are cached per tenant.
    /// </summary>
    Task<string> GetTokenAsync(string? tenantId = null, CancellationToken ct = default);

    /// <summary>Pre-fetch the home-tenant token so the first call:answer isn't
    /// slowed by it (the policy answer window is only ~5 seconds).</summary>
    Task WarmUpAsync(CancellationToken ct = default);
}

public sealed class GraphTokenProvider : IGraphTokenProvider
{
    private const string Scope = "https://graph.microsoft.com/.default";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly BotOptions _options;
    private readonly ILogger<GraphTokenProvider> _logger;
    private readonly ConcurrentDictionary<string, TenantToken> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public GraphTokenProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<BotOptions> options,
        ILogger<GraphTokenProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public Task WarmUpAsync(CancellationToken ct = default)
    {
        // We can't know customer tenants ahead of time, so only warm the home
        // tenant - and only when it's a concrete tenant (client-credentials can't
        // target /common or /organizations).
        var home = _options.TenantId;
        if (string.IsNullOrWhiteSpace(home) ||
            home.Equals("common", StringComparison.OrdinalIgnoreCase) ||
            home.Equals("organizations", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Skipping token warm-up: no concrete home tenant configured.");
            return Task.CompletedTask;
        }

        return GetTokenAsync(home, ct);
    }

    public async Task<string> GetTokenAsync(string? tenantId = null, CancellationToken ct = default)
    {
        var tenant = string.IsNullOrWhiteSpace(tenantId) ? _options.TenantId : tenantId;
        if (string.IsNullOrWhiteSpace(tenant) ||
            tenant.Equals("common", StringComparison.OrdinalIgnoreCase) ||
            tenant.Equals("organizations", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "No concrete tenant id available for token acquisition. Provide the calling " +
                "tenant id, or set Bot__TenantId to a specific tenant for fallback.");
        }

        var entry = _cache.GetOrAdd(tenant, _ => new TenantToken());

        // Refresh a minute before expiry to stay clear of the answer deadline.
        if (entry.IsFresh)
        {
            return entry.Token!;
        }

        await entry.Gate.WaitAsync(ct);
        try
        {
            if (entry.IsFresh)
            {
                return entry.Token!;
            }

            var client = _httpClientFactory.CreateClient();
            var tokenEndpoint = $"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token";

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

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException(
                    $"Token request for tenant {tenant} failed: {(int)response.StatusCode} {error}");
            }

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct)
                        ?? throw new InvalidOperationException("Empty token response from Entra.");

            entry.Token = token.AccessToken;
            entry.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn);
            _logger.LogInformation("Acquired Graph app token for tenant {Tenant}; expires at {ExpiresAt:o}.",
                tenant, entry.ExpiresAt);
            return entry.Token!;
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    /// <summary>Per-tenant cached token with its own refresh gate.</summary>
    private sealed class TenantToken
    {
        public readonly SemaphoreSlim Gate = new(1, 1);
        public string? Token;
        public DateTimeOffset ExpiresAt = DateTimeOffset.MinValue;

        public bool IsFresh => Token is not null && DateTimeOffset.UtcNow < ExpiresAt.AddMinutes(-1);
    }

    private sealed record TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }
}
