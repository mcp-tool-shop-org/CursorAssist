using CursorAssist.Engine.Core;

namespace CursorAssist.Engine.Transforms;

/// <summary>
/// Directional intent detection and boost transform.
///
/// Detects sustained intentional movement by measuring directional coherence
/// (cosine similarity) between consecutive velocity vectors. When coherence
/// exceeds a threshold over a smoothed window, adds a velocity-proportional
/// displacement in the detected direction.
///
/// Coherence math:
///   C = (v_n · v_{n-1}) / (|v_n| × |v_{n-1}|)
///   C > threshold → sustained direction (intentional)
///   C fluctuating → tremor / undirected motion
///
/// Boost math (when coherence exceeds threshold):
///   ramp = (coherenceEma − threshold) / (1 − threshold)
///   boostAmount = ramp × strength × BoostFactor
///   offset = unitDirection × boostAmount × currentSpeed
///
/// Only modifies X/Y — Dx/Dy are preserved for downstream transforms.
///
/// Pipeline position:
///   Raw → SoftDeadzone → Smoothing → PhaseCompensation → DirectionalIntent → Magnetism
///
/// Parameters from AssistiveConfig:
///   IntentBoostStrength — master strength [0, 1]. 0 = disabled.
///   IntentCoherenceThreshold — cosine similarity threshold (default 0.8).
/// </summary>
public sealed class DirectionalIntentTransform : IStatefulTransform
{
    public string TransformId => "assist.intent.directional";

    private float _prevDx;
    private float _prevDy;
    private float _coherenceEma;
    private bool _initialized;

    /// <summary>EMA beta for coherence smoothing: ~10-frame time constant at 60 Hz.
    /// β=0.15 → τ ≈ (1−0.15)/0.15 = 5.67 ticks ≈ 94ms.</summary>
    private const float CoherenceEmaBeta = 0.15f;

    /// <summary>Maximum additional displacement as fraction of current speed.</summary>
    private const float BoostFactor = 0.3f;

    /// <summary>Minimum velocity magnitude for meaningful coherence computation.</summary>
    private const float VelocityFloor = 0.1f;

    /// <summary>Minimum velocity to apply boost (prevents boosting coherent micro-jitter).</summary>
    private const float BoostVelocityMin = 0.5f;

    public InputSample Apply(in InputSample input, TransformContext context)
    {
        float strength = context.Config?.IntentBoostStrength ?? 0f;

        if (strength <= 0f)
        {
            // Disabled — still track deltas for warm start if enabled later
            _prevDx = input.Dx;
            _prevDy = input.Dy;
            _initialized = true;
            return input;
        }

        if (!_initialized)
        {
            _prevDx = input.Dx;
            _prevDy = input.Dy;
            _initialized = true;
            return input;
        }

        // Current and previous velocity magnitudes
        float vCurr = MathF.Sqrt(input.Dx * input.Dx + input.Dy * input.Dy);
        float vPrev = MathF.Sqrt(_prevDx * _prevDx + _prevDy * _prevDy);

        // Compute coherence (cosine similarity)
        float coherence = 0f;
        if (vCurr > VelocityFloor && vPrev > VelocityFloor)
        {
            float dot = input.Dx * _prevDx + input.Dy * _prevDy;
            coherence = dot / (vCurr * vPrev);
        }

        // Update EMA of coherence
        _coherenceEma += CoherenceEmaBeta * (coherence - _coherenceEma);

        // Store current deltas for next tick
        _prevDx = input.Dx;
        _prevDy = input.Dy;

        // Boost: if coherence above threshold, add velocity-proportional offset
        float threshold = context.Config?.IntentCoherenceThreshold ?? 0.8f;

        if (_coherenceEma > threshold && vCurr > BoostVelocityMin)
        {
            // Normalized boost ramp: 0 at threshold, 1 at coherence=1
            float ramp = (_coherenceEma - threshold) / (1f - threshold);
            float boostAmount = ramp * strength * BoostFactor;

            // Project in direction of current motion
            float nx = input.Dx / vCurr;
            float ny = input.Dy / vCurr;

            float outX = input.X + nx * boostAmount * vCurr;
            float outY = input.Y + ny * boostAmount * vCurr;

            return input with { X = outX, Y = outY };
        }

        return input;
    }

    public void Reset()
    {
        _prevDx = 0f;
        _prevDy = 0f;
        _coherenceEma = 0f;
        _initialized = false;
    }
}
