using System.Text.Json.Serialization;

namespace CursorAssist.Canon.Schemas;

/// <summary>
/// Captured motor-behavior metrics for a user. Immutable per version.
/// Version 1: baseline metrics from training or profiling sessions.
/// </summary>
public sealed record MotorProfile
{
    public const string SchemaId = "cursorassist.motor-profile";
    public const int SchemaVersion = 1;

    [JsonPropertyName("$schema")]
    public string Schema { get; init; } = SchemaId;

    [JsonPropertyName("$version")]
    public int Version { get; init; } = SchemaVersion;

    /// <summary>Unique profile identifier (e.g., user-generated or session-derived).</summary>
    [JsonPropertyName("profileId")]
    public required string ProfileId { get; init; }

    /// <summary>ISO-8601 timestamp of profile creation.</summary>
    [JsonPropertyName("createdUtc")]
    public required DateTimeOffset CreatedUtc { get; init; }

    // ── Tremor ──────────────────────────────────────────────

    /// <summary>Dominant tremor frequency in Hz (from high-freq delta variance). 0 = not measured.</summary>
    [JsonPropertyName("tremorFrequencyHz")]
    public float TremorFrequencyHz { get; init; }

    /// <summary>Tremor amplitude in virtual pixels (RMS of high-freq displacement). 0 = not measured.</summary>
    [JsonPropertyName("tremorAmplitudeVpx")]
    public float TremorAmplitudeVpx { get; init; }

    // ── Path quality ────────────────────────────────────────

    /// <summary>Mean path efficiency: ideal_distance / actual_distance. 1.0 = perfect straight line.</summary>
    [JsonPropertyName("pathEfficiency")]
    public float PathEfficiency { get; init; }

    /// <summary>Mean overshoot count per target acquisition.</summary>
    [JsonPropertyName("overshootRate")]
    public float OvershootRate { get; init; }

    /// <summary>Mean overshoot magnitude in virtual pixels.</summary>
    [JsonPropertyName("overshootMagnitudeVpx")]
    public float OvershootMagnitudeVpx { get; init; }

    // ── Timing ──────────────────────────────────────────────

    /// <summary>Mean time-to-target in seconds.</summary>
    [JsonPropertyName("meanTimeToTargetS")]
    public float MeanTimeToTargetS { get; init; }

    /// <summary>Standard deviation of time-to-target in seconds.</summary>
    [JsonPropertyName("stdDevTimeToTargetS")]
    public float StdDevTimeToTargetS { get; init; }

    // ── Click stability ─────────────────────────────────────

    /// <summary>Mean displacement during click hold (virtual pixels). Lower = more stable.</summary>
    [JsonPropertyName("clickStabilityVpx")]
    public float ClickStabilityVpx { get; init; }

    // ── Session context ─────────────────────────────────────

    /// <summary>Number of trials/targets that produced this profile.</summary>
    [JsonPropertyName("sampleCount")]
    public int SampleCount { get; init; }
}
