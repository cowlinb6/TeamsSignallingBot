using System.Collections.Concurrent;

namespace SignalingBot.Services;

/// <summary>
/// Remembers the last seen Graph state per call so transitions (e.g. hold ->
/// established = "Retrieved") can be derived. In-memory is fine for the PoC.
/// </summary>
public interface ICallStateTracker
{
    /// <summary>Records the new state and returns the previous one (null if first seen).</summary>
    string? Exchange(string callId, string newState);

    void Remove(string callId);
}

public sealed class CallStateTracker : ICallStateTracker
{
    private readonly ConcurrentDictionary<string, string> _states = new();

    public string? Exchange(string callId, string newState)
    {
        string? previous = _states.TryGetValue(callId, out var existing) ? existing : null;
        _states[callId] = newState;
        return previous;
    }

    public void Remove(string callId) => _states.TryRemove(callId, out _);
}
