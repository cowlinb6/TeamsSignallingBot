using System.Text.Json;
using SignalingBot.Models;

namespace SignalingBot.Services;

/// <summary>
/// Turns raw Graph calling notifications into normalized <see cref="CallEvent"/>
/// log lines and answers incoming policy calls.
/// </summary>
public interface INotificationProcessor
{
    /// <param name="tenantId">Calling tenant from the validated notification token,
    /// used to answer with a token from the right tenant (multi-tenant support).</param>
    Task ProcessAsync(CommsNotifications notifications, string? tenantId, CancellationToken ct = default);
}

public sealed class NotificationProcessor : INotificationProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ICallService _callService;
    private readonly ICallStateTracker _stateTracker;
    private readonly ILogger<NotificationProcessor> _logger;

    public NotificationProcessor(
        ICallService callService,
        ICallStateTracker stateTracker,
        ILogger<NotificationProcessor> logger)
    {
        _callService = callService;
        _stateTracker = stateTracker;
        _logger = logger;
    }

    public async Task ProcessAsync(CommsNotifications notifications, string? tenantId, CancellationToken ct = default)
    {
        foreach (var notification in notifications.Value)
        {
            try
            {
                if (notification.IsParticipantResource)
                {
                    LogParticipants(notification);
                }
                else
                {
                    await HandleCallNotificationAsync(notification, tenantId, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing notification for resource {Resource}.",
                    notification.ResourceUrl ?? notification.Resource);
            }
        }
    }

    private async Task HandleCallNotificationAsync(CommsNotification notification, string? tenantId, CancellationToken ct)
    {
        var call = notification.ResourceData.Deserialize<Call>(JsonOptions);
        var callId = call?.Id ?? notification.CallId;
        if (string.IsNullOrEmpty(callId))
        {
            return;
        }

        if (string.Equals(notification.ChangeType, "deleted", StringComparison.OrdinalIgnoreCase))
        {
            EmitEvent(new CallEvent { CallId = callId, Event = "Terminated", State = "terminated" });
            _stateTracker.Remove(callId);
            return;
        }

        var state = call?.State;
        if (string.IsNullOrEmpty(state))
        {
            return;
        }

        var previous = _stateTracker.Exchange(callId, state);
        if (string.Equals(previous, state, StringComparison.OrdinalIgnoreCase))
        {
            return; // no transition, nothing to report
        }

        // Auto-answer the policy-routed incoming call within the ~5s window,
        // using a token from the calling tenant.
        if (string.Equals(state, "incoming", StringComparison.OrdinalIgnoreCase))
        {
            await _callService.AnswerWithServiceHostedMediaAsync(callId, tenantId, ct);
        }

        EmitEvent(new CallEvent
        {
            CallId = callId,
            Event = MapEvent(state, previous),
            State = state,
            PreviousState = previous,
            Direction = call?.Direction,
            CallChainId = call?.CallChainId,
        });

        if (string.Equals(state, "terminated", StringComparison.OrdinalIgnoreCase))
        {
            _stateTracker.Remove(callId);
        }
    }

    /// <summary>Maps a Graph call state (plus the prior state) to a business event.</summary>
    private static string MapEvent(string state, string? previous) => state.ToLowerInvariant() switch
    {
        "incoming" => "Offered",
        "establishing" => "Establishing",
        // hold -> established is a resume; anything else into established is connect.
        "established" => string.Equals(previous, "hold", StringComparison.OrdinalIgnoreCase)
            ? "Retrieved"
            : "Established",
        "hold" => "Held",
        "transferring" => "Transferring",
        "redirecting" => "Redirecting",
        "terminating" => "Terminating",
        "terminated" => "Terminated",
        _ => state,
    };

    private void LogParticipants(CommsNotification notification)
    {
        var participants = ExtractParticipantNames(notification.ResourceData);
        EmitEvent(new CallEvent
        {
            CallId = notification.CallId ?? "unknown",
            Event = "ParticipantsChanged",
            Participants = participants,
        });
    }

    /// <summary>Best-effort pull of participant display names / ids from the roster payload.</summary>
    private static List<string> ExtractParticipantNames(JsonElement resourceData)
    {
        var names = new List<string>();

        IEnumerable<JsonElement> participants = resourceData.ValueKind switch
        {
            JsonValueKind.Array => resourceData.EnumerateArray(),
            JsonValueKind.Object => new[] { resourceData },
            _ => Array.Empty<JsonElement>(),
        };

        foreach (var participant in participants)
        {
            if (!participant.TryGetProperty("info", out var info) ||
                !info.TryGetProperty("identity", out var identity))
            {
                continue;
            }

            // identity is a keyed object (user / phone / guest / applicationInstance ...).
            foreach (var kind in identity.EnumerateObject())
            {
                if (kind.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var label = kind.Value.TryGetProperty("displayName", out var dn) && dn.ValueKind == JsonValueKind.String
                    ? dn.GetString()
                    : kind.Value.TryGetProperty("id", out var id) ? id.GetString() : null;

                if (!string.IsNullOrEmpty(label))
                {
                    names.Add($"{kind.Name}:{label}");
                }
            }
        }

        return names;
    }

    private void EmitEvent(CallEvent callEvent)
    {
        // One structured JSON line per event — easy to grep now, ship to a bus later.
        _logger.LogInformation("CALL_EVENT {Event}", JsonSerializer.Serialize(callEvent, JsonOptions));
    }
}
