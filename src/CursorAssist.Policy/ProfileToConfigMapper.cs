using CursorAssist.Canon.Schemas;

namespace CursorAssist.Policy;

/// <summary>
/// Deterministic mapper: MotorProfile -> AssistiveConfig.
/// Policy is versioned — same profile + same policy version = same config, always.
/// Extracted from Engine so that CLI tools and runtime can share without pulling in the pipeline.
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
        // Smoothing master strength: higher tremor -> more smoothing
        float smoothing = Clamp01(profile.TremorAmplitudeVpx / 10f);

        // Velocity-adaptive EMA parameters:
        // MinAlpha: lower for severe tremor (stronger suppression at rest)
        float minAlpha = MathF.Max(0.03f, 0.15f - profile.TremorAmplitudeVpx * 0.012f);

        // MaxAlpha: high to preserve responsiveness; slightly lower for poor path efficiency
        float maxAlpha = MathF.Min(0.95f, 0.90f + profile.PathEfficiency * 0.05f);

        // VelocityMax: lower threshold for higher tremor (tremor detected sooner)
        float vMax = MathF.Max(3f, 8f - profile.TremorAmplitudeVpx * 0.3f);

        // Prediction: more overshoot -> less prediction (avoid amplifying)
        float prediction = Clamp01(0.05f - profile.OvershootRate * 0.01f);
        if (prediction < 0f) prediction = 0f;

        // Magnetism radius: poor path efficiency -> larger radius
        float pathDeficiency = Clamp01(1f - profile.PathEfficiency);
        float magnetismRadius = 30f + pathDeficiency * 120f; // 30-150 vpx

        // Magnetism strength: higher tremor + worse path -> stronger pull
        float magnetismStrength = Clamp01(
            smoothing * 0.5f + pathDeficiency * 0.5f);

        // Hysteresis: proportional to radius
        float hysteresis = magnetismRadius * 0.15f;

        // Edge resistance: high overshoot -> more resistance
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
            SmoothingVelocityMax = vMax,
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
