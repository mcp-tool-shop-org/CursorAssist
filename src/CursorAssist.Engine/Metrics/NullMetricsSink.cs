using CursorAssist.Engine.Core;

namespace CursorAssist.Engine.Metrics;

/// <summary>
/// No-op metrics sink for when metrics collection is not needed.
/// </summary>
public sealed class NullMetricsSink : IMetricsSink
{
    public static readonly NullMetricsSink Instance = new();

    public void RecordTick(long tick, in InputSample raw, in InputSample transformed, IReadOnlyList<TargetInfo> targets) { }
    public void RecordEvent(in EngineEvent engineEvent) { }
    public void Reset() { }
}
