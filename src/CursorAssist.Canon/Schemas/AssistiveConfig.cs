using System.Text.Json.Serialization;

namespace CursorAssist.Canon.Schemas;

/// <summary>
/// Parameters for assistive cursor transforms. Derived from a MotorProfile
/// via a versioned mapping policy. Immutable per version.
/// </summary>
public sealed record AssistiveConfig
{
    public const string SchemaId = "cursorassist.assistive-config";
    public const int SchemaVersion = 2;

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

    /// <summary>
    /// User-selectable precision mode. When true, enables dual-pole (2nd-order EMA)
    /// regardless of the mapper's SmoothingDualPoleEnabled setting.
    /// This allows users with moderate tremor (2–4 vpx) to opt into −40 dB/decade
    /// suppression without needing to edit JSON config files.
    /// The mapper never auto-sets this; it is a user override only.
    /// Default false.
    /// </summary>
    [JsonPropertyName("precisionModeEnabled")]
    public bool PrecisionModeEnabled { get; init; }

    // ── Soft deadzone (magnitude-domain tremor suppression) ──
    //
    // Quadratic compression: r' = r² / (r + D)
    //   r ≪ D → r' ≈ r²/D (near zero, suppressed)
    //   r ≫ D → r' ≈ r (pass-through)
    //   Continuous, differentiable, no hard edge.
    //
    // D derived from TremorAmplitudeVpx and TremorFrequencyHz:
    //   D = k × A × √(f / f_ref), k=0.8, f_ref=8 Hz
    //   When f=0 (no measurement): D = k × A (amplitude-only fallback)
    // Pipeline: Raw → SoftDeadzone → SmoothingTransform → PhaseComp → Intent → Magnetism

    /// <summary>
    /// Quadratic deadzone compression radius in virtual pixels.
    /// Deltas with magnitude ≪ D are suppressed; magnitude ≫ D pass through.
    /// Formula: r' = r² / (r + D). Default 0 = disabled.
    /// Derived from TremorAmplitudeVpx. Range [0, 3.0].
    /// </summary>
    [JsonPropertyName("deadzoneRadiusVpx")]
    public float DeadzoneRadiusVpx { get; init; }

    // ── Phase compensation (EMA lag offset) ──────────────────
    //
    // Feed-forward velocity projection to offset EMA-induced phase lag.
    //   τ ≈ (1 − α) / α / Fs
    //   x_comp = x + gain_seconds × Dx × Fs
    //
    // gain is in seconds for narratability ("7ms lag compensation").

    /// <summary>
    /// Feed-forward lag compensation gain in seconds. 0 = disabled.
    /// Derived from average expected EMA alpha and a conservative 0.7× multiplier.
    /// Applied after SmoothingTransform: x_comp = x + gain × Dx × 60.
    /// Range [0, 0.1]. Typical: 0.007–0.015s (7–15ms compensation).
    /// </summary>
    [JsonPropertyName("phaseCompensationGainS")]
    public float PhaseCompensationGainS { get; init; }

    // ── Directional intent boost ─────────────────────────────
    //
    // Detects sustained intentional movement via cosine similarity
    // of consecutive velocity vectors. When coherence exceeds a
    // threshold, adds a velocity-proportional displacement boost
    // in the detected direction of motion.

    /// <summary>
    /// Directional intent boost strength [0, 1]. 0 = disabled.
    /// When > 0, detects sustained directional coherence and boosts
    /// cursor displacement in the direction of motion.
    /// Derived from PathEfficiency. Range [0, 1].
    /// </summary>
    [JsonPropertyName("intentBoostStrength")]
    public float IntentBoostStrength { get; init; }

    /// <summary>
    /// Cosine similarity threshold for directional intent detection.
    /// Above this threshold, boost engages. Default 0.8. Range [0.5, 1.0].
    /// </summary>
    [JsonPropertyName("intentCoherenceThreshold")]
    public float IntentCoherenceThreshold { get; init; } = 0.8f;

    /// <summary>
    /// Cosine similarity threshold for intent boost disengagement.
    /// Below this threshold, boost disengages. Must be ≤ IntentCoherenceThreshold.
    /// The gap between engage and disengage thresholds creates a hysteresis band
    /// that prevents flicker when coherence oscillates near the threshold.
    /// Default 0.65 (gap of 0.15 ≈ 1 EMA time constant at β=0.15).
    /// Range [0.3, 1.0].
    /// </summary>
    [JsonPropertyName("intentDisengageThreshold")]
    public float IntentDisengageThreshold { get; init; } = 0.65f;

    /// <summary>
    /// Prediction horizon in seconds. Reserved for a future speculative-advance
    /// transform. No engine transform currently reads this field — set it to 0.
    /// The mapper derives a value for future compatibility, but it has no
    /// effect on output until a prediction transform is implemented.
    /// </summary>
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
