using MouseTrainer.Domain.Runs;
using MouseTrainer.Simulation.Levels;
using MouseTrainer.Simulation.Modes.ReflexGates;

namespace MouseTrainer.Simulation.Mutators;

/// <summary>
/// Applies small deterministic vertical offsets to each gate's RestCenterY,
/// breaking the generator's smooth center-variation pattern.
/// Offsets are derived from WallX and Phase (no RNG, fully reproducible).
/// Parameter: "str" (float, range [0, 1], default 0.35).
/// Clamps results to keep gates within corridor-safe margins.
/// </summary>
public sealed class GateJitterMutator : IBlueprintMutator
{
    /// <summary>Maximum jitter offset in virtual pixels at str=1.0.</summary>
    private const float MaxJitterPx = 25f;

    /// <summary>Minimum margin from corridor edge to gate center (in virtual pixels).</summary>
    private const float CorridorMargin = 10f;

    /// <summary>Frequency multiplier for the sinusoidal jitter pattern.</summary>
    private const float JitterFreq = 0.015f;

    private readonly float _strength;

    public GateJitterMutator(float strength = 0.35f)
    {
        if (strength < 0f || strength > 1f)
            throw new ArgumentOutOfRangeException(nameof(strength),
                $"GateJitter strength must be in [0, 1]. Got: {strength}");
        _strength = strength;
    }

    public GateJitterMutator(MutatorSpec spec)
        : this(GetStrength(spec)) { }

    public LevelBlueprint Apply(LevelBlueprint blueprint)
    {
        float halfHeight = blueprint.PlayfieldHeight * 0.5f;
        var gates = new Gate[blueprint.Gates.Count];

        for (int i = 0; i < blueprint.Gates.Count; i++)
        {
            var src = blueprint.Gates[i];

            // Deterministic offset from WallX + Phase (varies per gate, no RNG)
            float offset = MathF.Sin(src.WallX * JitterFreq + src.Phase)
                * _strength * MaxJitterPx;

            float newCenterY = src.RestCenterY + offset;

            // Clamp to corridor-safe range: gate center must leave room for
            // half the aperture + margin on both sides
            float halfAperture = src.ApertureHeight * 0.5f;
            float minY = halfAperture + CorridorMargin;
            float maxY = blueprint.PlayfieldHeight - halfAperture - CorridorMargin;

            if (minY > maxY)
            {
                // Aperture too large for corridor — center it
                newCenterY = halfHeight;
            }
            else
            {
                newCenterY = MathF.Max(minY, MathF.Min(maxY, newCenterY));
            }

            gates[i] = new Gate
            {
                WallX = src.WallX,
                RestCenterY = newCenterY,
                ApertureHeight = src.ApertureHeight,
                Amplitude = src.Amplitude,
                Phase = src.Phase,
                FreqHz = src.FreqHz,
            };
        }

        return new LevelBlueprint
        {
            Gates = gates,
            PlayfieldWidth = blueprint.PlayfieldWidth,
            PlayfieldHeight = blueprint.PlayfieldHeight,
            ScrollSpeed = blueprint.ScrollSpeed,
        };
    }

    private static float GetStrength(MutatorSpec spec)
    {
        for (int i = 0; i < spec.Params.Count; i++)
        {
            if (spec.Params[i].Key == "str")
                return spec.Params[i].Value;
        }
        return 0.35f;
    }
}
