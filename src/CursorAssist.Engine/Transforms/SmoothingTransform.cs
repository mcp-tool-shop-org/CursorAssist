using CursorAssist.Engine.Core;

namespace CursorAssist.Engine.Transforms;

/// <summary>
/// Exponential moving average (EMA) smoothing transform.
/// Reduces tremor/jitter without requiring target knowledge.
/// Parameters sourced from AssistiveConfig.SmoothingStrength.
///
/// This is the v0 runtime assist transform — works everywhere,
/// no UIA or target discovery needed.
/// </summary>
public sealed class SmoothingTransform : IInputTransform
{
    public string TransformId => "assist.smoothing";

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

        // EMA: alpha = 1 - strength. Lower alpha = more smoothing.
        // strength=0 → alpha=1 (no smoothing), strength=1 → alpha=0 (full lag)
        // Clamp to [0.05, 1.0] to prevent complete freezing
        float alpha = MathF.Max(0.05f, 1f - strength);

        _smoothX = _smoothX + alpha * (input.X - _smoothX);
        _smoothY = _smoothY + alpha * (input.Y - _smoothY);

        return input with { X = _smoothX, Y = _smoothY };
    }

    public void Reset()
    {
        _smoothX = 0f;
        _smoothY = 0f;
        _initialized = false;
    }
}
