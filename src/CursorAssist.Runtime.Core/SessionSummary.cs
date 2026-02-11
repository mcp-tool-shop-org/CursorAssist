using System.Text.Json.Serialization;

namespace CursorAssist.Runtime.Core;

/// <summary>
/// Session-level telemetry summary persisted as JSON after each pilot session.
/// Captures duration, tick counts, velocity stats, and exit conditions.
/// </summary>
public sealed record SessionSummary
{
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("startedUtc")]
    public required DateTimeOffset StartedUtc { get; init; }

    [JsonPropertyName("endedUtc")]
    public required DateTimeOffset EndedUtc { get; init; }

    [JsonPropertyName("durationSeconds")]
    public required float DurationSeconds { get; init; }

    /// <summary>FNV-1a hash of the serialized AssistiveConfig JSON, for deduplication.</summary>
    [JsonPropertyName("configHash")]
    public required string ConfigHash { get; init; }

    [JsonPropertyName("fixedHz")]
    public int FixedHz { get; init; } = 60;

    [JsonPropertyName("totalTicks")]
    public required long TotalTicks { get; init; }

    [JsonPropertyName("overrunCount")]
    public long OverrunCount { get; init; }

    /// <summary>Average cursor speed in vpx/tick over the session.</summary>
    [JsonPropertyName("meanVelocity")]
    public float MeanVelocity { get; init; }

    /// <summary>Peak instantaneous cursor speed in vpx/tick.</summary>
    [JsonPropertyName("peakVelocity")]
    public float PeakVelocity { get; init; }

    [JsonPropertyName("emergencyStopFired")]
    public bool EmergencyStopFired { get; init; }

    /// <summary>Exit reason: "user", "kill-switch", "error".</summary>
    [JsonPropertyName("exitReason")]
    public required string ExitReason { get; init; }
}
