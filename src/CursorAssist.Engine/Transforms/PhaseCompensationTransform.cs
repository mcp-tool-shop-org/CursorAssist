using CursorAssist.Engine.Core;

namespace CursorAssist.Engine.Transforms;

/// <summary>
/// Feed-forward phase compensation for EMA-induced lag.
///
/// EMA smoothing introduces phase lag: τ ≈ (1 − α) / α / Fs.
/// At α=0.3, Fs=60: τ ≈ 39ms. This transform projects the filtered
/// position forward by gain × velocity to partially offset that lag.
///
/// Math (v4: velocity-attenuated):
///   velocity = √(Dx² + Dy²)
///   effectiveGain = gainS / (1 + velocity / VelocitySaturation)
///   x_comp = x + effectiveGain × Dx × Fs
///   y_comp = y + effectiveGain × Dy × Fs
///
/// VelocitySaturation = 15 vpx/tick (900 px/s at 60 Hz). This provides
/// a soft saturation that prevents overshoot during rapid acceleration:
///   - v=1: 94% gain (near full compensation for slow tremor)
///   - v=5: 75% (mild attenuation during moderate motion)
///   - v=15: 50% (significant attenuation during fast motion)
///   - v=30: 33% (strong attenuation during ballistic moves)
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

    /// <summary>
    /// Soft saturation knee: at this velocity (vpx/tick), effective gain is halved.
    /// 15 vpx/tick = 900 px/s at 60 Hz — fast intentional motion.
    /// </summary>
    private const float VelocitySaturation = 15f;

    public InputSample Apply(in InputSample input, TransformContext context)
    {
        float gainS = context.Config?.PhaseCompensationGainS ?? 0f;

        if (gainS <= 0f)
            return input;

        // Velocity-dependent gain attenuation (v4):
        // effectiveGain = gainS / (1 + velocity / VelocitySaturation)
        // At low velocity: near-full compensation for EMA lag
        // At high velocity: attenuated to prevent overshoot during deceleration
        float velocity = MathF.Sqrt(input.Dx * input.Dx + input.Dy * input.Dy);
        float effectiveGain = gainS / (1f + velocity / VelocitySaturation);

        // Feed-forward: project position by effectiveGain × velocity
        // Dx is vpx/tick; velocity in vpx/s = Dx × Fs
        // Offset = effectiveGain × velocity_vpx_per_s = effectiveGain × Dx × Fs
        float compX = input.X + effectiveGain * input.Dx * Fs;
        float compY = input.Y + effectiveGain * input.Dy * Fs;

        return input with { X = compX, Y = compY };
    }

    public void Reset()
    {
        // Stateless — no internal state to reset.
    }
}
