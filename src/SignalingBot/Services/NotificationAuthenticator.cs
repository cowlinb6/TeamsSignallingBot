using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using SignalingBot.Configuration;

namespace SignalingBot.Services;

/// <summary>
/// Validates the Bearer token Microsoft attaches to each calling notification.
/// </summary>
public interface INotificationAuthenticator
{
    Task<bool> ValidateAsync(string? authorizationHeader, CancellationToken ct = default);
}

public sealed class NotificationAuthenticator : INotificationAuthenticator
{
    // Incoming policy/compliance call notifications arrive via the Azure Bot
    // calling webhook and are signed as Bot Connector tokens, whose keys live in
    // the Bot Framework OpenID metadata (login.botframework.com). Some call-scoped
    // / Graph-issued notifications are instead signed per the Skype metadata.
    // We therefore validate against the UNION of both keysets and accept either
    // issuer — validating against only one endpoint yields IDX10503 "kid not
    // found" for the other token type, which silently 401s the recorder and makes
    // Teams drop the call ("problem setting up the recording required by your org").
    private static readonly string[] OpenIdConfigUrls =
    {
        "https://login.botframework.com/v1/.well-known/openidconfiguration",
        "https://api.aps.skype.com/v1/.well-known/OpenIdConfiguration",
    };

    private static readonly string[] ValidIssuers =
    {
        "https://api.botframework.com",
        "https://graph.microsoft.com",
    };

    private readonly BotOptions _options;
    private readonly ILogger<NotificationAuthenticator> _logger;
    private readonly ConfigurationManager<OpenIdConnectConfiguration>[] _configManagers;
    private readonly JsonWebTokenHandler _handler = new();

    public NotificationAuthenticator(IOptions<BotOptions> options, ILogger<NotificationAuthenticator> logger)
    {
        _options = options.Value;
        _logger = logger;
        _configManagers = OpenIdConfigUrls
            .Select(url => new ConfigurationManager<OpenIdConnectConfiguration>(
                url,
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever()))
            .ToArray();
    }

    public async Task<bool> ValidateAsync(string? authorizationHeader, CancellationToken ct = default)
    {
        if (!_options.ValidateNotificationToken)
        {
            _logger.LogWarning("Notification token validation is DISABLED (local testing only).");
            return true;
        }

        if (string.IsNullOrWhiteSpace(authorizationHeader) ||
            !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Rejected notification: missing or malformed Authorization header.");
            return false;
        }

        var token = authorizationHeader["Bearer ".Length..].Trim();

        // First attempt uses cached metadata; on a signing-key miss, force a
        // refresh and retry once to ride out key rotation.
        return await TryValidateAsync(token, forceRefresh: false, ct)
            || await TryValidateAsync(token, forceRefresh: true, ct);
    }

    private async Task<bool> TryValidateAsync(string token, bool forceRefresh, CancellationToken ct)
    {
        var signingKeys = new List<SecurityKey>();
        foreach (var manager in _configManagers)
        {
            try
            {
                if (forceRefresh)
                {
                    manager.RequestRefresh();
                }

                var config = await manager.GetConfigurationAsync(ct);
                signingKeys.AddRange(config.SigningKeys);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load OpenID signing keys from one endpoint (continuing).");
            }
        }

        if (signingKeys.Count == 0)
        {
            _logger.LogWarning("Rejected notification: no signing keys available from any metadata endpoint.");
            return false;
        }

        var result = await _handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = ValidIssuers,
            ValidateAudience = true,
            ValidAudience = _options.ClientId,
            ValidateLifetime = true,
            IssuerSigningKeys = signingKeys,
            ValidateIssuerSigningKey = true,
        });

        if (result.IsValid)
        {
            return true;
        }

        // Stay quiet on the first (cached-key) miss; only warn once we've also
        // tried refreshed keys, so a single rotation doesn't spam the log.
        if (forceRefresh)
        {
            _logger.LogWarning(result.Exception, "Rejected notification: token validation failed.");
        }
        else
        {
            _logger.LogDebug(result.Exception, "Token failed on cached keys; refreshing and retrying.");
        }

        return false;
    }
}
