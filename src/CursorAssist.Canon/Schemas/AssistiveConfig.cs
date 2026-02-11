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

    // ── Smoothing (velocity-adaptive EMA) ────────────────────

    /// <summary>
    /// Master smoothing strength [0, 1]. 0 = disabled, 1 = maximum.
    /// Controls the overall intensity of velocity-adaptive EMA.
    /// When > 0, the filter uses MinAlpha at low velocity and MaxAlpha at high velocity.
    /// </summary>
    [JsonPropertyName("smoothingStrength")]
    public float SmoothingStrength { get; init; }

    /// <summary>
    /// Minimum alpha (strongest smoothing) applied at zero velocity.
    /// Lower values = heavier tremor suppression when cursor is nearly still.
    /// Range [0.01, 1]. Default 0.08. Only used when SmoothingStrength > 0.
    /// </summary>
    [JsonPropertyName("smoothingMinAlpha")]
    public float SmoothingMinAlpha { get; init; } = 0.08f;

    /// <summary>
    /// Maximum alpha (weakest smoothing) applied at or above VelocityMax.
    /// Higher values = more responsive during intentional fast motion.
    /// Range [0.01, 1]. Default 0.9. Only used when SmoothingStrength > 0.
    /// </summary>
    [JsonPropertyName("smoothingMaxAlpha")]
    public float SmoothingMaxAlpha { get; init; } = 0.9f;

    /// <summary>
    /// Velocity magnitude (vpx/tick) at which alpha reaches MaxAlpha.
    /// Derived from MotorProfile speed distribution. Default 8.0.
    /// </summary>
    [JsonPropertyName("smoothingVelocityMax")]
    public float SmoothingVelocityMax { get; init; } = 8f;

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
