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

    // ── Smoothing (velocity-adaptive 1st-order IIR low-pass) ──
    //
    // EMA is a 1st-order IIR filter with -3dB cutoff:
    //   fc ≈ α·Fs / (2π)   (Fs = 60 Hz engine tick rate)
    //
    // Target suppression band: 4–12 Hz (physiological/essential tremor)
    // Preserve: <3 Hz intentional motion, >12 Hz flick transitions
    //
    // α ≈ 0.25 → fc ≈ 2.4 Hz (strong tremor suppression)
    // α ≈ 0.63 → fc ≈ 6 Hz   (moderate)
    // α ≈ 0.90 → fc ≈ 8.6 Hz (minimal smoothing, near pass-through)

    /// <summary>
    /// Master smoothing strength [0, 1]. 0 = disabled, 1 = maximum.
    /// Controls the overall intensity of velocity-adaptive EMA.
    /// When > 0, the filter uses MinAlpha at low velocity and MaxAlpha at high velocity.
    /// </summary>
    [JsonPropertyName("smoothingStrength")]
    public float SmoothingStrength { get; init; }

    /// <summary>
    /// Minimum alpha (strongest smoothing) applied at or below VelocityLow.
    /// Maps to fc ≈ α·60/(2π). Default 0.25 → fc ≈ 2.4 Hz.
    /// Range [0.05, 1]. Avoid below 0.05 — feels "underwater" (fc &lt; 0.5 Hz).
    /// </summary>
    [JsonPropertyName("smoothingMinAlpha")]
    public float SmoothingMinAlpha { get; init; } = 0.25f;

    /// <summary>
    /// Maximum alpha (weakest smoothing) applied at or above VelocityHigh.
    /// Default 0.9 → fc ≈ 8.6 Hz (passes nearly everything).
    /// Range [0.05, 1].
    /// </summary>
    [JsonPropertyName("smoothingMaxAlpha")]
    public float SmoothingMaxAlpha { get; init; } = 0.9f;

    /// <summary>
    /// Velocity magnitude (vpx/tick) below which alpha stays at MinAlpha.
    /// Motion below this threshold is treated as tremor/micro-jitter.
    /// Default 0.5 vpx/tick (≈ 30 px/s at 60 Hz).
    /// </summary>
    [JsonPropertyName("smoothingVelocityLow")]
    public float SmoothingVelocityLow { get; init; } = 0.5f;

    /// <summary>
    /// Velocity magnitude (vpx/tick) at or above which alpha reaches MaxAlpha.
    /// Motion above this is treated as intentional — minimal filtering.
    /// Default 8.0 vpx/tick (≈ 480 px/s at 60 Hz).
    /// </summary>
    [JsonPropertyName("smoothingVelocityHigh")]
    public float SmoothingVelocityHigh { get; init; } = 8f;

    /// <summary>
    /// When true, SmoothingTransform estimates tremor frequency in real-time
    /// via zero-crossing rate and dynamically adjusts MinAlpha using the
    /// closed-form law: α_min = Clamp(2πk/Fs × f_est, 0.20, 0.40).
    /// When false, uses the static MinAlpha from the mapper.
    /// Default false for deterministic benchmarking with fixed parameters.
    /// </summary>
    [JsonPropertyName("smoothingAdaptiveFrequencyEnabled")]
    public bool SmoothingAdaptiveFrequencyEnabled { get; init; }

    /// <summary>
    /// When true, applies 2nd-order EMA (two cascaded poles) at velocities
    /// ≤ VelocityLow for −40 dB/decade rolloff (vs −20 dB/decade single-pole).
    /// Blends to single-pole between VelocityLow and VelocityHigh.
    /// Default false (single-pole only). Enabled for high tremor amplitude.
    /// </summary>
    [JsonPropertyName("smoothingDualPoleEnabled")]
    public bool SmoothingDualPoleEnabled { get; init; }

    // ── Soft deadzone (magnitude-domain tremor suppression) ──
    //
    // Quadratic compression: r' = r² / (r + D)
    //   r ≪ D → r' ≈ r²/D (near zero, suppressed)
    //   r ≫ D → r' ≈ r (pass-through)
    //   Continuous, differentiable, no hard edge.
    //
    // D derived from TremorAmplitudeVpx: D = k × A, k=1.0
    // Pipeline: Raw → SoftDeadzone → SmoothingTransform → Magnetism

    /// <summary>
    /// Quadratic deadzone compression radius in virtual pixels.
    /// Deltas with magnitude ≪ D are suppressed; magnitude ≫ D pass through.
    /// Formula: r' = r² / (r + D). Default 0 = disabled.
    /// Derived from TremorAmplitudeVpx. Range [0, 3.0].
    /// </summary>
    [JsonPropertyName("deadzoneRadiusVpx")]
    public float DeadzoneRadiusVpx { get; init; }

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
