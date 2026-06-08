using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignalingBot.Models;

/// <summary>
/// Envelope Microsoft Graph POSTs to the calling webhook:
/// <c>{ "@odata.type": "#microsoft.graph.commsNotifications", "value": [ ... ] }</c>.
/// </summary>
public sealed class CommsNotifications
{
    [JsonPropertyName("value")]
    public List<CommsNotification> Value { get; set; } = new();
}

/// <summary>A single change notification within the envelope.</summary>
public sealed class CommsNotification
{
    /// <summary>created | updated | deleted</summary>
    [JsonPropertyName("changeType")]
    public string? ChangeType { get; set; }

    /// <summary>e.g. <c>/communications/calls/{id}</c> or <c>.../participants</c>.</summary>
    [JsonPropertyName("resourceUrl")]
    public string? ResourceUrl { get; set; }

    [JsonPropertyName("resource")]
    public string? Resource { get; set; }

    /// <summary>
    /// The changed resource (a call, or a participant collection). Kept as a raw
    /// element so we can type only the fields we need and log the rest verbatim.
    /// </summary>
    [JsonPropertyName("resourceData")]
    public JsonElement ResourceData { get; set; }

    /// <summary>True when this notification concerns a participant roster change.</summary>
    public bool IsParticipantResource =>
        (ResourceUrl ?? Resource ?? string.Empty).Contains("/participants", StringComparison.OrdinalIgnoreCase);

    /// <summary>Extracts the call id from the resource path, if present.</summary>
    public string? CallId
    {
        get
        {
            var path = ResourceUrl ?? Resource ?? string.Empty;
            const string marker = "/calls/";
            var start = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return null;
            start += marker.Length;
            var end = path.IndexOf('/', start);
            return end < 0 ? path[start..] : path[start..end];
        }
    }
}
