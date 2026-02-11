using CursorAssist.Engine.Core;

namespace CursorAssist.Engine.Transforms;

/// <summary>
/// Magnitude-domain soft deadzone using quadratic compression.
///
/// Unlike time-domain EMA filtering, this suppresses small tremor-scale
/// deltas without introducing phase lag into larger intentional movements.
///
/// Formula: r' = r² / (r + D)
///   r = delta magnitude = √(Dx² + Dy²)
///   D = deadzone radius (from AssistiveConfig.DeadzoneRadiusVpx)
///
/// Behavior:
///   r ≪ D → r' ≈ r²/D (quadratic suppression, near zero)
///   r ≫ D → r' ≈ r − D (approaches pass-through)
///   r = D → r' = D/2  (50% suppression at the knee)
///
/// Continuous, differentiable at r=0 — no hard edge, no discontinuity.
///
/// Placed before SmoothingTransform in the pipeline:
///   Raw → SoftDeadzone → VelocityAdaptiveEMA → Magnetism
///
/// Parameters from AssistiveConfig:
///   DeadzoneRadiusVpx — compression radius (0 = disabled)
/// </summary>
public sealed class SoftDeadzoneTransform : IInputTransform
{
    public string TransformId => "assist.deadzone.soft-quadratic";

    private float _prevOutX;
    private float _prevOutY;
    private bool _initialized;

    public InputSample Apply(in InputSample input, TransformContext context)
    {
        var config = context.Config;
        float D = config?.DeadzoneRadiusVpx ?? 0f;

        if (D <= 0f)
        {
            // Disabled — pass through, but track position
            _prevOutX = input.X;
            _prevOutY = input.Y;
            _initialized = true;
            return input;
        }

        if (!_initialized)
        {
            _prevOutX = input.X;
            _prevOutY = input.Y;
            _initialized = true;
            return input;
        }

        float dx = input.Dx;
        float dy = input.Dy;
        float r = MathF.Sqrt(dx * dx + dy * dy);

        if (r < 1e-6f)
        {
            // No movement — hold position
            return input with { X = _prevOutX, Y = _prevOutY };
        }

        // Quadratic compression: scale = r / (r + D)
        // Output magnitude: r' = r * scale = r² / (r + D)
        float scale = r / (r + D);

        float outDx = dx * scale;
        float outDy = dy * scale;
        float outX = _prevOutX + outDx;
        float outY = _prevOutY + outDy;

        _prevOutX = outX;
        _prevOutY = outY;

        return input with { X = outX, Y = outY, Dx = outDx, Dy = outDy };
    }

    public void Reset()
    {
        _prevOutX = 0f;
        _prevOutY = 0f;
        _initialized = false;
    }
}
