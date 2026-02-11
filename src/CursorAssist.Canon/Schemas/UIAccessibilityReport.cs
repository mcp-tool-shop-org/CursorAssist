using System.Text.Json.Serialization;

namespace CursorAssist.Canon.Schemas;

/// <summary>
/// Accessibility benchmark report for a UI layout. Produced by running
/// trials (baseline and/or assisted) against a layout with a motor profile.
/// </summary>
public sealed record UIAccessibilityReport
{
    public const string SchemaId = "cursorassist.ui-accessibility-report";
    public const int SchemaVersion = 1;

    [JsonPropertyName("$schema")]
    public string Schema { get; init; } = SchemaId;

    [JsonPropertyName("$version")]
    public int Version { get; init; } = SchemaVersion;

    /// <summary>ISO-8601 timestamp of report generation.</summary>
    [JsonPropertyName("generatedUtc")]
    public required DateTimeOffset GeneratedUtc { get; init; }

    /// <summary>Layout identifier (filename or logical name).</summary>
    [JsonPropertyName("layoutId")]
    public required string LayoutId { get; init; }

    /// <summary>Profile used for the trials (null for unassisted baseline).</summary>
    [JsonPropertyName("profileId")]
    public string? ProfileId { get; init; }

    /// <summary>Whether assistive transforms were active.</summary>
    [JsonPropertyName("assisted")]
    public bool Assisted { get; init; }

    /// <summary>Number of trials executed.</summary>
    [JsonPropertyName("trialCount")]
    public int TrialCount { get; init; }

    // ── Aggregate metrics ───────────────────────────────────

    /// <summary>Mean effective target width (Fitts' law We).</summary>
    [JsonPropertyName("meanEffectiveWidthVpx")]
    public float MeanEffectiveWidthVpx { get; init; }

    /// <summary>Mean Fitts' Law index of difficulty (bits).</summary>
    [JsonPropertyName("meanIndexOfDifficulty")]
    public float MeanIndexOfDifficulty { get; init; }

    /// <summary>Overall error rate [0, 1].</summary>
    [JsonPropertyName("errorRate")]
    public float ErrorRate { get; init; }

    /// <summary>Mean time-to-target in seconds.</summary>
    [JsonPropertyName("meanTimeToTargetS")]
    public float MeanTimeToTargetS { get; init; }

    /// <summary>Mean path efficiency [0, 1]. 1.0 = optimal.</summary>
    [JsonPropertyName("meanPathEfficiency")]
    public float MeanPathEfficiency { get; init; }

    /// <summary>Throughput in bits/s (Fitts' Law IP).</summary>
    [JsonPropertyName("throughputBitsPerS")]
    public float ThroughputBitsPerS { get; init; }

    // ── Per-target breakdown ────────────────────────────────

    /// <summary>Per-target results. Null if summary-only mode.</summary>
    [JsonPropertyName("targets")]
    public IReadOnlyList<TargetResult>? Targets { get; init; }
}

/// <summary>
/// Benchmark result for a single target in the layout.
/// </summary>
public sealed record TargetResult
{
    [JsonPropertyName("targetId")]
    public required string TargetId { get; init; }

    [JsonPropertyName("effectiveWidthVpx")]
    public float EffectiveWidthVpx { get; init; }

    [JsonPropertyName("indexOfDifficulty")]
    public float IndexOfDifficulty { get; init; }

    [JsonPropertyName("errorRate")]
    public float ErrorRate { get; init; }

    [JsonPropertyName("meanTimeToTargetS")]
    public float MeanTimeToTargetS { get; init; }

    [JsonPropertyName("pathEfficiency")]
    public float PathEfficiency { get; init; }
}
