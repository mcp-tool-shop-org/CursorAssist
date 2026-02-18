using CursorAssist.Canon.Schemas;

namespace CursorAssist.Policy;

/// <summary>
/// Deterministic mapper: MotorProfile -> AssistiveConfig.
/// Policy is versioned — same profile + same policy version = same config, always.
/// Extracted from Engine so that CLI tools and runtime can share without pulling in the pipeline.
/// </summary>
public static class ProfileToConfigMapper
{
    public const int PolicyVersion = 4;

    // Closed-form frequency → alpha mapping constant:
    //   α_min = Clamp(FreqToAlphaK × f_tremor, 0.20, 0.40)
    //   Derivation: fc = k × f_tremor (cutoff at half the tremor frequency)
    //   α = 2π × fc / Fs = 2π × k × f_tremor / Fs
    //   For k=0.5, Fs=60: FreqToAlphaK = 2π × 0.5 / 60 ≈ 0.05236
    private const float FreqToAlphaK = 0.05236f;

    /// <summary>
    /// Map a motor profile to an assistive configuration.
    /// v4 policy: power-law freq-weighted deadzone, phase compensation, directional intent.
    /// </summary>
    public static AssistiveConfig Map(MotorProfile profile)
    {
        // Smoothing master strength: higher tremor -> more smoothing
        float smoothing = Clamp01(profile.TremorAmplitudeVpx / 10f);

        // Velocity-adaptive EMA parameters (DSP-grounded for 60 Hz):
        //   fc ≈ α·60/(2π)
        //   α=0.20 -> fc≈1.9Hz, α=0.35 -> fc≈3.3Hz (strong suppression band)
        //   α=0.85 -> fc≈8.1Hz, α=0.95 -> fc≈9.1Hz (near pass-through)

        // MinAlpha: closed-form from tremor frequency when available
        //   α_min places -3dB cutoff at half the tremor frequency (k=0.5)
        //   e.g. f_tremor=6 Hz → fc=3 Hz → α≈0.314
        //   Clamped to [0.20, 0.40] to stay in safe suppression band
        float minAlpha;
        bool hasFrequency = profile.TremorFrequencyHz > 0f;
        if (hasFrequency)
        {
            minAlpha = Math.Clamp(FreqToAlphaK * profile.TremorFrequencyHz, 0.20f, 0.40f);
        }
        else
        {
            // Fallback: amplitude-based (for profiles without frequency measurement)
            minAlpha = MathF.Max(0.20f, 0.35f - profile.TremorAmplitudeVpx * 0.015f);
        }

        // MaxAlpha: high to preserve responsiveness; slightly lower for poor path efficiency
        float maxAlpha = MathF.Min(0.95f, 0.85f + profile.PathEfficiency * 0.1f);

        // VelocityLow: tremor ceiling — motion below this is treated as tremor
        // Higher tremor amplitude -> higher vLow (tremor produces larger micro-deltas)
        float vLow = MathF.Max(0.3f, 0.5f + profile.TremorAmplitudeVpx * 0.1f);

        // VelocityHigh: intentional motion floor — above this, minimal filtering
        // Lower for higher tremor (more of the velocity range gets filtered)
        float vHigh = MathF.Max(vLow + 1f, 10f - profile.TremorAmplitudeVpx * 0.5f);

        // Prediction: more overshoot -> less prediction (avoid amplifying).
        // Reserved — PredictionHorizonS is written for future compatibility but
        // no engine transform reads it yet. No behavioral effect currently.
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

        // Soft deadzone: D = k × A × (f / f_ref)^p
        // k=0.8, f_ref=8 Hz, p=0.65 (v4: power-law replaces v3 sqrt)
        // Power-law exponent 0.65 is between sqrt (0.5) and linear (1.0):
        //   - Relaxes suppression at low frequencies (3–4 Hz) where tremor
        //     overlaps with slow intentional motion
        //   - Tightens suppression at high frequencies (12+ Hz) where small
        //     per-tick deltas accumulate more destabilization
        //   - Range expands from 1.73:1 (sqrt) to 2.12:1 across [4, 12] Hz
        // When f=0 (no measurement): fall back to D = k × A (amplitude-only)
        // Disable for negligible tremor (< 0.5 vpx)
        float deadzoneRadius;
        if (profile.TremorAmplitudeVpx > 0.5f)
        {
            const float kDz = 0.8f;
            const float fRef = 8f;
            const float FreqExponent = 0.65f;
            float freqWeight = profile.TremorFrequencyHz > 0f
                ? MathF.Pow(profile.TremorFrequencyHz / fRef, FreqExponent)
                : 1f;
            deadzoneRadius = Math.Clamp(kDz * profile.TremorAmplitudeVpx * freqWeight, 0.2f, 3.0f);
        }
        else
        {
            deadzoneRadius = 0f;
        }

        // Dual-pole: enable for significant tremor amplitude (> 4 vpx)
        // Provides -40 dB/decade at low velocity for precision modes
        bool dualPole = profile.TremorAmplitudeVpx > 4f;

        // Phase compensation: offset EMA lag with feed-forward velocity projection
        // τ_avg = (1 − avgAlpha) / avgAlpha / Fs
        // Conservative gain: 0.7× to avoid overshoot
        // v4: frequency-aware attenuation — when minAlpha is high (≥0.40, i.e. high-freq
        // tremor at 12+ Hz), the EMA is barely filtering and there is little lag to
        // compensate. Phase comp at high frequency just amplifies noise, so we attenuate
        // it down to zero. Linear ramp from full gain at minAlpha≤0.30 to zero at minAlpha≥0.40.
        float phaseCompGainS;
        if (smoothing >= 0.1f)
        {
            float avgAlpha = (minAlpha + maxAlpha) / 2f;
            float lagS = (1f - avgAlpha) / avgAlpha / 60f;
            float freqAttenuation = 1f - Clamp01((minAlpha - 0.30f) / 0.10f);
            phaseCompGainS = lagS * 0.7f * freqAttenuation;
        }
        else
        {
            phaseCompGainS = 0f; // Negligible smoothing — no lag to compensate
        }

        // Directional intent boost: only for users with reasonable path efficiency
        // Poor path efficiency + boost = amplified errors
        float intentBoostStrength = profile.PathEfficiency > 0.6f
            ? Clamp01(profile.PathEfficiency - 0.4f)
            : 0f;
        float intentCoherenceThreshold = 0.8f;
        float intentDisengageThreshold = MathF.Max(0.50f, intentCoherenceThreshold - 0.15f);

        return new AssistiveConfig
        {
            SourceProfileId = profile.ProfileId,
            MappingPolicyVersion = PolicyVersion,
            SmoothingStrength = smoothing,
            SmoothingMinAlpha = minAlpha,
            SmoothingMaxAlpha = maxAlpha,
            SmoothingVelocityLow = vLow,
            SmoothingVelocityHigh = vHigh,
            SmoothingAdaptiveFrequencyEnabled = hasFrequency,
            SmoothingDualPoleEnabled = dualPole,
            DeadzoneRadiusVpx = deadzoneRadius,
            PhaseCompensationGainS = phaseCompGainS,
            IntentBoostStrength = intentBoostStrength,
            IntentCoherenceThreshold = intentCoherenceThreshold,
            IntentDisengageThreshold = intentDisengageThreshold,
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
