using System.Text.Json.Serialization;

namespace CursorAssist.Canon.Schemas;

/// <summary>
/// Parameters for assistive cursor transforms. Derived from a MotorProfile
/// via a versioned mapping policy. Immutable per version.
/// </summary>
public sealed record AssistiveConfig
{
    public const string SchemaId = "cursorassist.assistive-config";
    public const int SchemaVersion = 1;

    [JsonPropertyName("$schema")]
    public string Schema { get; init; } = SchemaId;

    [JsonPropertyName("$version")]
    public int Version { get; init; } = SchemaVersion;

    /// <summary>ID of the MotorProfile this config was derived from.</summary>
    [JsonPropertyName("sourceProfileId")]
    public required string SourceProfileId { get; init; }

    /// <summary>Version of the mapping policy that produced this config.</summary>
    [JsonPropertyName("mappingPolicyVersion")]
    public int MappingPolicyVersion { get; init; } = 1;

    // ── Smoothing ───────────────────────────────────────────

    /// <summary>Low-pass smoothing strength [0, 1]. 0 = no smoothing, 1 = maximum.</summary>
    [JsonPropertyName("smoothingStrength")]
    public float SmoothingStrength { get; init; }

    /// <summary>Prediction horizon in seconds. 0 = no prediction.</summary>
    [JsonPropertyName("predictionHorizonS")]
    public float PredictionHorizonS { get; init; }

    // ── Target magnetism ────────────────────────────────────

    /// <summary>Activation radius in virtual pixels. Magnetism engages within this distance.</summary>
    [JsonPropertyName("magnetismRadiusVpx")]
    public float MagnetismRadiusVpx { get; init; }

    /// <summary>Magnetism pull strength [0, 1]. 0 = off, 1 = full snap.</summary>
    [JsonPropertyName("magnetismStrength")]
    public float MagnetismStrength { get; init; }

    /// <summary>Hysteresis margin in virtual pixels. Prevents flicker at boundary.</summary>
    [JsonPropertyName("magnetismHysteresisVpx")]
    public float MagnetismHysteresisVpx { get; init; }

    // ── Edge / snap ─────────────────────────────────────────

    /// <summary>Edge resistance strength [0, 1]. Slows cursor near target edges.</summary>
    [JsonPropertyName("edgeResistance")]
    public float EdgeResistance { get; init; }

    /// <summary>Snap radius in virtual pixels. Below this distance, snap to center.</summary>
    [JsonPropertyName("snapRadiusVpx")]
    public float SnapRadiusVpx { get; init; }
}
