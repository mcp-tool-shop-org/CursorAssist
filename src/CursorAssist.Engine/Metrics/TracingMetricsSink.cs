using CursorAssist.Engine.Core;
using CursorAssist.Trace;

namespace CursorAssist.Engine.Metrics;

/// <summary>
/// Metrics sink that writes per-tick samples to a TraceWriter for
/// tick-level telemetry logging (opt-in via --trace flag).
///
/// Also tracks running session statistics:
///   - Total tick count
///   - Cumulative and peak cursor velocity
///
/// Thread-safe: RecordTick may be called from the engine thread while
/// ExportStats is called from the main thread at session end.
/// </summary>
public sealed class TracingMetricsSink : IMetricsSink
{
    private readonly TraceWriter _writer;
    private readonly object _lock = new();

    // Running statistics
    private long _tickCount;
    private double _velocitySum;
    private float _peakVelocity;

    public TracingMetricsSink(TraceWriter writer)
    {
        _writer = writer;
    }

    public void RecordTick(long tick, in InputSample raw, in InputSample transformed,
        IReadOnlyList<TargetInfo> targets)
    {
        // Compute velocity magnitude from transformed deltas
        float velMag = MathF.Sqrt(transformed.Dx * transformed.Dx +
                                  transformed.Dy * transformed.Dy);

        byte buttons = 0;
        if (transformed.PrimaryDown) buttons |= 1;
        if (transformed.SecondaryDown) buttons |= 2;

        var sample = new TraceSample
        {
            Tick = (int)tick,
            X = transformed.X,
            Y = transformed.Y,
            Dx = transformed.Dx,
            Dy = transformed.Dy,
            Buttons = buttons
        };

        lock (_lock)
        {
            _writer.WriteSample(in sample);
            _tickCount++;
            _velocitySum += velMag;
            if (velMag > _peakVelocity) _peakVelocity = velMag;
        }
    }

    public void RecordEvent(in EngineEvent engineEvent)
    {
        // Events are not written to trace (trace is tick-only).
        // Could be extended in future to interleave event records.
    }

    public void Reset()
    {
        lock (_lock)
        {
            _tickCount = 0;
            _velocitySum = 0;
            _peakVelocity = 0f;
        }
    }

    /// <summary>
    /// Export accumulated session statistics.
    /// </summary>
    /// <returns>Tuple of (totalTicks, meanVelocity vpx/tick, peakVelocity vpx/tick).</returns>
    public (long TotalTicks, float MeanVelocity, float PeakVelocity) ExportStats()
    {
        lock (_lock)
        {
            float mean = _tickCount > 0 ? (float)(_velocitySum / _tickCount) : 0f;
            return (_tickCount, mean, _peakVelocity);
        }
    }
}
