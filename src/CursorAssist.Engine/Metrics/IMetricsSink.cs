using CursorAssist.Engine.Core;

namespace CursorAssist.Engine.Metrics;

/// <summary>
/// Collects per-tick metrics and events from the engine pipeline.
/// Implementations decide what to aggregate (profiling, benchmarking, logging).
/// </summary>
public interface IMetricsSink
{
    /// <summary>Record a raw + transformed sample pair for a tick.</summary>
    void RecordTick(long tick, in InputSample raw, in InputSample transformed, IReadOnlyList<TargetInfo> targets);

    /// <summary>Record an engine event.</summary>
    void RecordEvent(in EngineEvent engineEvent);

    /// <summary>Reset accumulated metrics (e.g., on session start).</summary>
    void Reset();
}
