using CursorAssist.Engine.Core;

namespace CursorAssist.Engine.Transforms;

/// <summary>
/// Feed-forward phase compensation for EMA-induced lag.
///
/// EMA smoothing introduces phase lag: τ ≈ (1 − α) / α / Fs.
/// At α=0.3, Fs=60: τ ≈ 39ms. This transform projects the filtered
/// position forward by gain × velocity to partially offset that lag.
///
/// Math:
///   x_comp = x + gain_seconds × Dx × Fs
///   y_comp = y + gain_seconds × Dy × Fs
///
/// gain_seconds is PhaseCompensationGainS from AssistiveConfig.
/// Dx, Dy are per-tick displacements (vpx/tick).
/// Fs = 60 Hz (ticks per second).
///
/// This is a purely feed-forward (stateless) transform.
/// When velocity ≈ 0, compensation vanishes — no instability at rest.
///
/// Pipeline position:
///   Raw → SoftDeadzone → Smoothing → PhaseCompensation → DirectionalIntent → Magnetism
///
/// Parameters from AssistiveConfig:
///   PhaseCompensationGainS — gain in seconds. 0 = disabled.
/// </summary>
public sealed class PhaseCompensationTransform : IInputTransform
{
    public string TransformId => "assist.phase-compensation";

    private const int Fs = 60;

    public InputSample Apply(in InputSample input, TransformContext context)
    {
        float gainS = context.Config?.PhaseCompensationGainS ?? 0f;

        if (gainS <= 0f)
            return input;

        // Feed-forward: project position by gain × velocity
        // Dx is vpx/tick; velocity in vpx/s = Dx × Fs
        // Offset = gainS × velocity_vpx_per_s = gainS × Dx × Fs
        float compX = input.X + gainS * input.Dx * Fs;
        float compY = input.Y + gainS * input.Dy * Fs;

        return input with { X = compX, Y = compY };
    }

    public void Reset()
    {
        // Stateless — no internal state to reset.
    }
}
