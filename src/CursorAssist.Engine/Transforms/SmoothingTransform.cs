using CursorAssist.Engine.Core;

namespace CursorAssist.Engine.Transforms;

/// <summary>
/// Velocity-adaptive 1st-order IIR low-pass (EMA) smoothing transform.
///
/// DSP foundation:
///   EMA is a 1st-order IIR filter: H(z) = α / (1 - (1-α)z⁻¹)
///   Approximate -3dB cutoff: fc ≈ α·Fs / (2π)   where Fs = 60 Hz
///
///   α ≈ 0.25 → fc ≈ 2.4 Hz (strong tremor suppression)
///   α ≈ 0.63 → fc ≈ 6.0 Hz (moderate)
///   α ≈ 0.90 → fc ≈ 8.6 Hz (near pass-through)
///
/// Target band: suppress 4–12 Hz tremor, preserve &lt;3 Hz intentional motion.
///
/// Velocity-adaptive strategy (two-breakpoint):
///   |v| ≤ vLow  → alpha = minAlpha (full tremor suppression)
///   |v| ≥ vHigh → alpha = maxAlpha (near pass-through)
///   vLow &lt; |v| &lt; vHigh → SmoothStep interpolation
///
/// Parameters from AssistiveConfig:
///   SmoothingStrength   — master control [0,1]; 0 = disabled
///   SmoothingMinAlpha   — alpha at/below vLow  (default 0.25 → fc ≈ 2.4 Hz)
///   SmoothingMaxAlpha   — alpha at/above vHigh (default 0.90 → fc ≈ 8.6 Hz)
///   SmoothingVelocityLow  — tremor velocity ceiling (default 0.5 vpx/tick)
///   SmoothingVelocityHigh — intentional motion floor (default 8.0 vpx/tick)
///
/// No target knowledge required. Universal base-layer filter.
/// </summary>
public sealed class SmoothingTransform : IInputTransform
{
    public string TransformId => "assist.smoothing.velocity-adaptive";

    private float _smoothX;
    private float _smoothY;
    private bool _initialized;

    public InputSample Apply(in InputSample input, TransformContext context)
    {
        var config = context.Config;
        float strength = config?.SmoothingStrength ?? 0f;

        if (strength <= 0f)
        {
            // No smoothing — just track raw position
            _smoothX = input.X;
            _smoothY = input.Y;
            _initialized = true;
            return input;
        }

        if (!_initialized)
        {
            _smoothX = input.X;
            _smoothY = input.Y;
            _initialized = true;
            return input;
        }

        // Compute velocity magnitude from per-tick deltas
        float velocity = MathF.Sqrt(input.Dx * input.Dx + input.Dy * input.Dy);

        // Read velocity-adaptive parameters (with safe defaults)
        float minAlpha = config!.SmoothingMinAlpha;
        float maxAlpha = config.SmoothingMaxAlpha;
        float vLow = config.SmoothingVelocityLow;
        float vHigh = config.SmoothingVelocityHigh;

        // Guard: ensure sane ranges
        if (minAlpha < 0.05f) minAlpha = 0.25f;
        if (maxAlpha <= minAlpha) maxAlpha = MathF.Max(minAlpha + 0.01f, 0.9f);
        if (vLow < 0f) vLow = 0.5f;
        if (vHigh <= vLow) vHigh = MathF.Max(vLow + 1f, 8f);

        // Two-breakpoint velocity-adaptive alpha:
        //   velocity ≤ vLow  → minAlpha (full tremor suppression)
        //   velocity ≥ vHigh → maxAlpha (near pass-through)
        //   between → SmoothStep interpolation
        float baseAlpha;
        if (velocity <= vLow)
        {
            baseAlpha = minAlpha;
        }
        else if (velocity >= vHigh)
        {
            baseAlpha = maxAlpha;
        }
        else
        {
            float t = (velocity - vLow) / (vHigh - vLow);
            float smooth = t * t * (3f - 2f * t); // Hermite smoothstep
            baseAlpha = minAlpha + (maxAlpha - minAlpha) * smooth;
        }

        // Apply master strength: strength=1 uses full adaptive range,
        // strength<1 biases alpha upward toward 1 (less smoothing overall)
        // alpha = Lerp(1.0, baseAlpha, strength)
        float alpha = 1f + strength * (baseAlpha - 1f);

        // Final clamp to prevent freeze or overshoot
        alpha = MathF.Max(0.05f, MathF.Min(1f, alpha));

        _smoothX += alpha * (input.X - _smoothX);
        _smoothY += alpha * (input.Y - _smoothY);

        return input with { X = _smoothX, Y = _smoothY };
    }

    public void Reset()
    {
        _smoothX = 0f;
        _smoothY = 0f;
        _initialized = false;
    }
}
