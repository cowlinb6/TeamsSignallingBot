namespace SignalingBot.Configuration;

/// <summary>
/// Configuration for the signaling bot. Bound from the "Bot" config section
/// (appsettings / environment variables, e.g. Bot__ClientId).
/// </summary>
public sealed class BotOptions
{
    /// <summary>Entra tenant ID hosting the app registration.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>App registration (and Azure Bot) Application/Client ID.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>App registration client secret (use a secret store in production).</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Public base URL of this bot (e.g. the ngrok HTTPS URL). The calling
    /// callback is this value + "/api/calling".
    /// </summary>
    public string PublicUrl { get; set; } = string.Empty;

    /// <summary>
    /// When true, inbound notification Bearer tokens are validated against the
    /// published OpenID configuration. Disable ONLY for early local plumbing
    /// tests — never in a reachable deployment.
    /// </summary>
    public bool ValidateNotificationToken { get; set; } = true;

    /// <summary>Callback URI sent on answer; defaults to PublicUrl + /api/calling.</summary>
    public string CallbackUri =>
        $"{PublicUrl.TrimEnd('/')}/api/calling";
}
