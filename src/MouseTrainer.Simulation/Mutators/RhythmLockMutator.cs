using MouseTrainer.Domain.Runs;
using MouseTrainer.Simulation.Levels;
using MouseTrainer.Simulation.Modes.ReflexGates;

namespace MouseTrainer.Simulation.Mutators;

/// <summary>
/// Quantizes each gate's Phase to the nearest of N evenly-spaced divisions around the circle.
/// Creates learnable rhythmic patterns — gates cluster into synchronous "chords."
/// Parameter: "div" (float-as-int, allowed values: {2, 3, 4, 6, 8}, default 4).
/// Idempotent: applying twice produces the same result.
/// </summary>
public sealed class RhythmLockMutator : IBlueprintMutator
{
    private static readonly HashSet<int> AllowedDivisions = new() { 2, 3, 4, 6, 8 };

    private readonly int _divisions;
    private readonly float _step; // 2π / divisions

    public RhythmLockMutator(int divisions = 4)
    {
        if (!AllowedDivisions.Contains(divisions))
            throw new ArgumentOutOfRangeException(nameof(divisions),
                $"RhythmLock divisions must be one of {{2, 3, 4, 6, 8}}. Got: {divisions}");
        _divisions = divisions;
        _step = 2f * MathF.PI / _divisions;
    }

    public RhythmLockMutator(MutatorSpec spec)
        : this(GetDivisions(spec)) { }

    public LevelBlueprint Apply(LevelBlueprint blueprint)
    {
        var gates = new Gate[blueprint.Gates.Count];
        for (int i = 0; i < blueprint.Gates.Count; i++)
        {
            var src = blueprint.Gates[i];
            gates[i] = new Gate
            {
                WallX = src.WallX,
                RestCenterY = src.RestCenterY,
                ApertureHeight = src.ApertureHeight,
                Amplitude = src.Amplitude,
                Phase = QuantizePhase(src.Phase),
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

    /// <summary>
    /// Snap phase to nearest step, normalize to [0, 2π).
    /// Tie-break: floor(x + 0.5f) for deterministic nearest-rounding.
    /// </summary>
    private float QuantizePhase(float phase)
    {
        // Normalize to [0, 2π)
        float twoPi = 2f * MathF.PI;
        float normalized = phase % twoPi;
        if (normalized < 0f) normalized += twoPi;

        // Snap to nearest division
        float k = MathF.Floor(normalized / _step + 0.5f);
        float snapped = k * _step;

        // Normalize result to [0, 2π)
        snapped %= twoPi;
        if (snapped < 0f) snapped += twoPi;

        return snapped;
    }

    private static int GetDivisions(MutatorSpec spec)
    {
        for (int i = 0; i < spec.Params.Count; i++)
        {
            if (spec.Params[i].Key == "div")
                return (int)spec.Params[i].Value;
        }
        return 4;
    }
}
