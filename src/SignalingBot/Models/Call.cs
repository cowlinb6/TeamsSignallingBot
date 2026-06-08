using System.Text.Json.Serialization;

namespace SignalingBot.Models;

/// <summary>
/// Minimal projection of the Graph <c>call</c> resource — only the fields the PoC
/// reads. The full payload is preserved separately for logging.
/// </summary>
public sealed class Call
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>incoming | establishing | established | hold | transferring |
    /// redirecting | terminating | terminated | unknownFutureValue</summary>
    [JsonPropertyName("state")]
    public string? State { get; set; }

    /// <summary>incoming | outgoing</summary>
    [JsonPropertyName("direction")]
    public string? Direction { get; set; }

    [JsonPropertyName("callChainId")]
    public string? CallChainId { get; set; }
}
