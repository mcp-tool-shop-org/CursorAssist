using CursorAssist.Canon.Schemas;

namespace CursorAssist.Engine.Mapping;

/// <summary>
/// Deterministic mapper: MotorProfile → AssistiveConfig.
/// Policy is versioned — same profile + same policy version = same config.
/// </summary>
public static class ProfileToConfigMapper
{
    public const int PolicyVersion = 1;

    /// <summary>
    /// Map a motor profile to an assistive configuration.
    /// v1 policy: linear scaling from profile metrics.
    /// </summary>
    public static AssistiveConfig Map(MotorProfile profile)
    {
        // Smoothing master strength: higher tremor → more smoothing
        float smoothing = Clamp01(profile.TremorAmplitudeVpx / 10f);

        // Velocity-adaptive EMA parameters (DSP-grounded for 60 Hz):
        //   fc ≈ α·60/(2π)
        //   α=0.20 → fc≈1.9Hz, α=0.35 → fc≈3.3Hz (strong suppression band)
        //   α=0.85 → fc≈8.1Hz, α=0.95 → fc≈9.1Hz (near pass-through)

        // MinAlpha: lower for severe tremor (deeper into 2–3 Hz cutoff)
        // Range: [0.20, 0.35] — clamp to never go below fc≈1.9 Hz
        float minAlpha = MathF.Max(0.20f, 0.35f - profile.TremorAmplitudeVpx * 0.015f);

        // MaxAlpha: high to preserve responsiveness; slightly lower for poor path efficiency
        float maxAlpha = MathF.Min(0.95f, 0.85f + profile.PathEfficiency * 0.1f);

        // VelocityLow: tremor ceiling — motion below this is treated as tremor
        // Higher tremor amplitude → higher vLow (tremor produces larger micro-deltas)
        float vLow = MathF.Max(0.3f, 0.5f + profile.TremorAmplitudeVpx * 0.1f);

        // VelocityHigh: intentional motion floor — above this, minimal filtering
        // Lower for higher tremor (more of the velocity range gets filtered)
        float vHigh = MathF.Max(vLow + 1f, 10f - profile.TremorAmplitudeVpx * 0.5f);

        // Prediction: more overshoot → less prediction (avoid amplifying)
        float prediction = Clamp01(0.05f - profile.OvershootRate * 0.01f);
        if (prediction < 0f) prediction = 0f;

        // Magnetism radius: poor path efficiency → larger radius
        float pathDeficiency = Clamp01(1f - profile.PathEfficiency);
        float magnetismRadius = 30f + pathDeficiency * 120f; // 30–150 vpx

        // Magnetism strength: higher tremor + worse path → stronger pull
        float magnetismStrength = Clamp01(
            smoothing * 0.5f + pathDeficiency * 0.5f);

        // Hysteresis: proportional to radius
        float hysteresis = magnetismRadius * 0.15f;

        // Edge resistance: high overshoot → more resistance
        float edgeResistance = Clamp01(profile.OvershootRate * 0.3f);

        // Snap radius: only for significant tremor
        float snapRadius = profile.TremorAmplitudeVpx > 3f ? 5f : 0f;

        return new AssistiveConfig
        {
            SourceProfileId = profile.ProfileId,
            MappingPolicyVersion = PolicyVersion,
            SmoothingStrength = smoothing,
            SmoothingMinAlpha = minAlpha,
            SmoothingMaxAlpha = maxAlpha,
            SmoothingVelocityLow = vLow,
            SmoothingVelocityHigh = vHigh,
            PredictionHorizonS = prediction,
            MagnetismRadiusVpx = magnetismRadius,
            MagnetismStrength = magnetismStrength,
            MagnetismHysteresisVpx = hysteresis,
            EdgeResistance = edgeResistance,
            SnapRadiusVpx = snapRadius
        };
    }

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
}
