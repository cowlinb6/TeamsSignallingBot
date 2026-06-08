using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SignalingBot.Configuration;

namespace SignalingBot.Services;

/// <summary>Performs Graph call control actions (answer) via REST.</summary>
public interface ICallService
{
    Task AnswerWithServiceHostedMediaAsync(string callId, string? tenantId, CancellationToken ct = default);
}

public sealed class CallService : ICallService
{
    private const string GraphBase = "https://graph.microsoft.com/v1.0";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IGraphTokenProvider _tokenProvider;
    private readonly BotOptions _options;
    private readonly ILogger<CallService> _logger;

    public CallService(
        IHttpClientFactory httpClientFactory,
        IGraphTokenProvider tokenProvider,
        IOptions<BotOptions> options,
        ILogger<CallService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _tokenProvider = tokenProvider;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Answers a policy-routed incoming call WITHOUT capturing media. This is the
    /// crux experiment: service-hosted media means Microsoft hosts the media and
    /// we only receive signaling — no Windows media stack required.
    /// </summary>
    public async Task AnswerWithServiceHostedMediaAsync(string callId, string? tenantId, CancellationToken ct = default)
    {
        // Answer with a token from the CALLING tenant; for a multi-tenant recorder
        // a home-tenant token would be rejected for other tenants' calls.
        var token = await _tokenProvider.GetTokenAsync(tenantId, ct);

        // https://learn.microsoft.com/graph/api/call-answer
        var body = new
        {
            callbackUri = _options.CallbackUri,
            acceptedModalities = new[] { "audio" },
            mediaConfig = new Dictionary<string, object?>
            {
                ["@odata.type"] = "#microsoft.graph.serviceHostedMediaConfig",
                ["preFetchMedia"] = Array.Empty<object>(),
            },
        };

        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{GraphBase}/communications/calls/{callId}/answer")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
        {
            // answer is async on Graph's side (202); state changes arrive via webhook.
            _logger.LogInformation("Answered call {CallId} with service-hosted media ({Status}).",
                callId, (int)response.StatusCode);
        }
        else
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Failed to answer call {CallId}: {Status} {Body}",
                callId, (int)response.StatusCode, content);
        }
    }
}
