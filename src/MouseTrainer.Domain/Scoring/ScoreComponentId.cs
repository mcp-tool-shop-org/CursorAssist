namespace MouseTrainer.Domain.Scoring;

/// <summary>
/// Identifies a scoring component. String-based for forward-compat.
/// Same permanent protocol rules as MutatorId: once created, frozen forever.
/// </summary>
public readonly record struct ScoreComponentId(string Value)
{
    /// <summary>Precision gate pass (or miss with Amount=0).</summary>
    public static readonly ScoreComponentId GateScore = new("GateScore");

    /// <summary>Reserved: combo multiplier bonus (future).</summary>
    public static readonly ScoreComponentId ComboBonus = new("ComboBonus");

    /// <summary>Reserved: explicit miss penalty (future).</summary>
    public static readonly ScoreComponentId MissPenalty = new("MissPenalty");

    public override string ToString() => Value;
}
