using CursorAssist.Canon.Schemas;
using CursorAssist.Engine.Core;

namespace CursorAssist.Engine.Transforms;

/// <summary>
/// Velocity-adaptive EMA smoothing transform.
///
/// Core insight: tremor is high-frequency / low-amplitude oscillation near zero
/// net velocity. Intentional movement is sustained, coherent, higher-velocity.
///
/// Strategy:
///   alpha = f(|velocity|)
///   Low velocity  → small alpha → strong smoothing (suppress tremor)
///   High velocity → large alpha → minimal smoothing (preserve responsiveness)
///
/// The velocity-to-alpha mapping uses SmoothStep for a natural transition curve.
///
/// Parameters from AssistiveConfig:
///   SmoothingStrength  — master control [0,1]; 0 = disabled
///   SmoothingMinAlpha  — alpha at zero velocity (strongest smoothing)
///   SmoothingMaxAlpha  — alpha at VelocityMax (weakest smoothing)
///   SmoothingVelocityMax — velocity magnitude where alpha reaches MaxAlpha
///
/// No target knowledge required. This is the universal base-layer filter.
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
        float vMax = config.SmoothingVelocityMax;

        // Guard: ensure sane ranges
        if (minAlpha <= 0f) minAlpha = 0.08f;
        if (maxAlpha <= minAlpha) maxAlpha = MathF.Max(minAlpha + 0.01f, 0.9f);
        if (vMax <= 0f) vMax = 8f;

        // Velocity-adaptive alpha via SmoothStep
        float t = Clamp01(velocity / vMax);
        float smooth = t * t * (3f - 2f * t); // Hermite smoothstep
        float baseAlpha = minAlpha + (maxAlpha - minAlpha) * smooth;

        // Apply master strength: strength=1 uses full adaptive range,
        // strength<1 biases alpha upward toward 1 (less smoothing overall)
        // alpha = Lerp(1.0, baseAlpha, strength)
        float alpha = 1f + strength * (baseAlpha - 1f);

        // Final clamp to prevent freeze or overshoot
        alpha = MathF.Max(0.01f, MathF.Min(1f, alpha));

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

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
}
