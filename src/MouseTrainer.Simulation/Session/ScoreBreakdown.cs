using MouseTrainer.Domain.Scoring;

namespace MouseTrainer.Simulation.Session;

/// <summary>
/// A single scored event in the auditable ledger.
/// One delta per gate encounter (pass or miss).
/// </summary>
public readonly record struct ScoreDelta(
    ScoreComponentId Component,
    int Amount,
    int GateIndex,
    int ComboAfter);

/// <summary>
/// Aggregated score breakdown computed from the delta ledger at session end.
/// Conservation invariant: TotalsByComponent.Values.Sum() == Total.
/// </summary>
public sealed record ScoreBreakdown(
    int Total,
    IReadOnlyDictionary<ScoreComponentId, int> TotalsByComponent,
    int DeltaCount)
{
    /// <summary>
    /// Build a breakdown from a list of deltas.
    /// </summary>
    internal static ScoreBreakdown FromDeltas(IReadOnlyList<ScoreDelta> deltas)
    {
        int total = 0;
        var byComponent = new Dictionary<ScoreComponentId, int>();

        foreach (var d in deltas)
        {
            total += d.Amount;

            if (byComponent.TryGetValue(d.Component, out int existing))
                byComponent[d.Component] = existing + d.Amount;
            else
                byComponent[d.Component] = d.Amount;
        }

        return new ScoreBreakdown(total, byComponent, deltas.Count);
    }
}
