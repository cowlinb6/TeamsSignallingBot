using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using SignalingBot.Configuration;

namespace SignalingBot.Services;

/// <summary>
/// Validates the Bearer token Graph attaches to each calling notification.
/// Per Microsoft guidance the token is signed per the published OpenID config,
/// issued by https://api.botframework.com, with <c>aud</c> = the bot App ID.
/// </summary>
public interface INotificationAuthenticator
{
    Task<bool> ValidateAsync(string? authorizationHeader, CancellationToken ct = default);
}

public sealed class NotificationAuthenticator : INotificationAuthenticator
{
    // Published OpenID configuration used to verify calling-notification tokens.
    private const string OpenIdConfigUrl = "https://api.aps.skype.com/v1/.well-known/OpenIdConfiguration";
    private const string Issuer = "https://api.botframework.com";

    private readonly BotOptions _options;
    private readonly ILogger<NotificationAuthenticator> _logger;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configManager;
    private readonly JsonWebTokenHandler _handler = new();

    public NotificationAuthenticator(IOptions<BotOptions> options, ILogger<NotificationAuthenticator> logger)
    {
        _options = options.Value;
        _logger = logger;
        _configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            OpenIdConfigUrl,
            new OpenIdConnectConfigurationRetriever());
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

        try
        {
            var config = await _configManager.GetConfigurationAsync(ct);
            var result = await _handler.ValidateTokenAsync(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = Issuer,
                ValidateAudience = true,
                ValidAudience = _options.ClientId,
                ValidateLifetime = true,
                IssuerSigningKeys = config.SigningKeys,
                ValidateIssuerSigningKey = true,
            });

            if (!result.IsValid)
            {
                _logger.LogWarning(result.Exception, "Rejected notification: token validation failed.");
            }

            return result.IsValid;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Rejected notification: error validating token.");
            return false;
        }
    }
}
