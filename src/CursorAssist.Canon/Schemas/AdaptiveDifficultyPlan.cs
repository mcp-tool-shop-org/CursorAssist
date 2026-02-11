using System.Text.Json.Serialization;

namespace CursorAssist.Canon.Schemas;

/// <summary>
/// Plan for adapting training difficulty based on a MotorProfile.
/// Consumed by training modes to generate next-session parameters.
/// </summary>
public sealed record AdaptiveDifficultyPlan
{
    public const string SchemaId = "cursorassist.adaptive-difficulty-plan";
    public const int SchemaVersion = 1;

    [JsonPropertyName("$schema")]
    public string Schema { get; init; } = SchemaId;

    [JsonPropertyName("$version")]
    public int Version { get; init; } = SchemaVersion;

    /// <summary>ID of the MotorProfile that produced this plan.</summary>
    [JsonPropertyName("sourceProfileId")]
    public required string SourceProfileId { get; init; }

    /// <summary>ISO-8601 timestamp of plan generation.</summary>
    [JsonPropertyName("generatedUtc")]
    public required DateTimeOffset GeneratedUtc { get; init; }

    // ── Target scaling rules ────────────────────────────────

    /// <summary>Target size multiplier [0.25, 4.0]. 1.0 = default.</summary>
    [JsonPropertyName("targetSizeMultiplier")]
    public float TargetSizeMultiplier { get; init; } = 1.0f;

    /// <summary>Target speed multiplier [0.25, 4.0]. 1.0 = default.</summary>
    [JsonPropertyName("targetSpeedMultiplier")]
    public float TargetSpeedMultiplier { get; init; } = 1.0f;

    // ── Progression ─────────────────────────────────────────

    /// <summary>Recommended difficulty tier (0 = easiest).</summary>
    [JsonPropertyName("recommendedTier")]
    public int RecommendedTier { get; init; }

    /// <summary>Whether to promote difficulty next session.</summary>
    [JsonPropertyName("shouldPromote")]
    public bool ShouldPromote { get; init; }

    /// <summary>Whether to demote difficulty next session.</summary>
    [JsonPropertyName("shouldDemote")]
    public bool ShouldDemote { get; init; }

    // ── Focus areas ─────────────────────────────────────────

    /// <summary>Training focus recommendations (e.g., "overshoot", "precision", "speed").</summary>
    [JsonPropertyName("focusAreas")]
    public IReadOnlyList<string> FocusAreas { get; init; } = [];

    /// <summary>Suggested mutator overrides for next session.</summary>
    [JsonPropertyName("suggestedMutators")]
    public IReadOnlyList<SuggestedMutator> SuggestedMutators { get; init; } = [];
}

/// <summary>
/// A mutator suggestion within an adaptive plan.
/// </summary>
public sealed record SuggestedMutator
{
    [JsonPropertyName("mutatorId")]
    public required string MutatorId { get; init; }

    [JsonPropertyName("parameters")]
    public IReadOnlyDictionary<string, float> Parameters { get; init; } = new Dictionary<string, float>();
}
