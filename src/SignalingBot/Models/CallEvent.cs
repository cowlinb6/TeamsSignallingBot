namespace SignalingBot.Models;

/// <summary>
/// The structured telephony event this PoC emits — one per meaningful transition.
/// Serialized as a single JSON log line so it can be shipped to any sink later.
/// </summary>
public sealed record CallEvent
{
    public required string CallId { get; init; }

    /// <summary>Normalized event name: Offered, Establishing, Established, Held,
    /// Retrieved, Transferring, Redirecting, Terminating, Terminated,
    /// ParticipantsChanged.</summary>
    public required string Event { get; init; }

    /// <summary>Raw Graph call state that produced this event, when applicable.</summary>
    public string? State { get; init; }

    public string? PreviousState { get; init; }

    public string? Direction { get; init; }

    public string? CallChainId { get; init; }

    /// <summary>Participant display names / ids for roster changes.</summary>
    public IReadOnlyList<string>? Participants { get; init; }

    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
}
