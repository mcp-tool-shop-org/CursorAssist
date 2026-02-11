using System.Diagnostics;
using CursorAssist.Engine.Core;
using CursorAssist.Runtime.Core;
using Xunit;

namespace CursorAssist.Tests.Runtime;

/// <summary>
/// Tests for the injection echo guard: ring buffer with timestamp-based time window.
/// Verifies that the dual-check (value tolerance + time window) correctly identifies
/// recently injected deltas while ignoring stale or non-matching entries.
/// </summary>
public class EchoGuardTests
{
    private static EngineThread MakeEngine() =>
        new(new TransformPipeline());

    [Fact]
    public void RecentInjection_WithinWindow_Detected()
    {
        var engine = MakeEngine();
        long now = Stopwatch.GetTimestamp();

        engine.RecordInjectedDelta(3.5f, -2.1f, now);

        Assert.True(engine.WasRecentlyInjected(3.5f, -2.1f, now));
    }

    [Fact]
    public void RecentInjection_OutsideWindow_NotDetected()
    {
        var engine = MakeEngine();
        long now = Stopwatch.GetTimestamp();

        // Record with a timestamp far in the past (1 second ago)
        long oldTimestamp = now - Stopwatch.Frequency; // 1 second ago (well past 50ms window)
        engine.RecordInjectedDelta(3.5f, -2.1f, oldTimestamp);

        Assert.False(engine.WasRecentlyInjected(3.5f, -2.1f, now));
    }

    [Fact]
    public void NonMatchingDelta_WithinWindow_NotDetected()
    {
        var engine = MakeEngine();
        long now = Stopwatch.GetTimestamp();

        engine.RecordInjectedDelta(5f, 5f, now);

        // Query with different delta values
        Assert.False(engine.WasRecentlyInjected(3f, 3f, now));
    }

    [Fact]
    public void RingBufferWraparound_OldEntriesOverwritten()
    {
        var engine = MakeEngine();
        long now = Stopwatch.GetTimestamp();

        // Record the entry we want to test
        engine.RecordInjectedDelta(1f, 1f, now);

        // Overwrite entire ring buffer (8 entries) with different values
        for (int i = 0; i < 8; i++)
        {
            engine.RecordInjectedDelta(99f + i, 99f + i, now);
        }

        // Original entry should have been overwritten
        Assert.False(engine.WasRecentlyInjected(1f, 1f, now));
    }
}
