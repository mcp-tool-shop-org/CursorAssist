using CursorAssist.Engine.Core;
using Xunit;

namespace CursorAssist.Tests.Runtime;

/// <summary>
/// Tests for the accumulator catch-up cap in DeterministicPipeline.Step().
/// Verifies that excess accumulator time from stalls is discarded rather than
/// causing burst spikes on subsequent frames.
/// </summary>
public class AccumulatorCapTests
{
    // Note: DeterministicPipeline.Step() uses _lastHostTicks == 0 as a "first frame" sentinel.
    // Tests must use a non-zero initial hostNowTicks to avoid triggering the sentinel twice.

    [Fact]
    public void LargeTimeGap_CapsStepsAndDiscardsExcess()
    {
        // Use maxStepsPerFrame=3 and 60Hz for easier math.
        // At 60Hz, fixedDt = 16.667ms. 3 steps max = 50ms of accumulator drained.
        var pipeline = new TransformPipeline();
        var engine = new DeterministicPipeline(pipeline, fixedHz: 60, maxStepsPerFrame: 3);

        long ticksPerSec = 10_000; // 10,000 ticks/sec for easy math
        var raw = new InputSample(100f, 100f, 0f, 0f, false, false, 0);

        // Frame 1: Initialize (non-zero timestamp to avoid sentinel guard)
        engine.Step(in raw, [], hostNowTicks: 1000, ticksPerSecond: ticksPerSec);

        // Frame 2: Normal frame to establish baseline tick count
        engine.Step(in raw, [], hostNowTicks: 1167, ticksPerSecond: ticksPerSec);
        long baselineTick = engine.CurrentTick;

        // Frame 3: Simulate a 200ms stall (2000 ticks at 10k/s)
        // 200ms at 60Hz = ~12 ticks of debt. With cap=3, only 3 ticks execute.
        var raw2 = new InputSample(105f, 100f, 5f, 0f, false, false, 1);
        engine.Step(in raw2, [], hostNowTicks: 3167, ticksPerSecond: ticksPerSec);

        long tickAfterStall = engine.CurrentTick;
        Assert.True(tickAfterStall <= baselineTick + 3,
            $"After 200ms stall with cap=3, should add at most 3 ticks. Baseline={baselineTick}, got {tickAfterStall}");

        // Frame 4: Normal frame (17ms later = ~1 tick)
        var raw3 = new InputSample(107f, 100f, 2f, 0f, false, false, 2);
        engine.Step(in raw3, [], hostNowTicks: 3337, ticksPerSecond: ticksPerSec);

        long tickAfterNormal = engine.CurrentTick;
        // Without the fix, the 9 excess ticks would burst here.
        // With the fix, at most 1-2 ticks execute (17ms + ≤16.67ms carryover).
        Assert.True(tickAfterNormal <= tickAfterStall + 2,
            $"Normal frame after stall should add ≤2 ticks, got {tickAfterNormal - tickAfterStall}");
    }

    [Fact]
    public void OverrunCounter_Increments_OnStall()
    {
        var pipeline = new TransformPipeline();
        var engine = new DeterministicPipeline(pipeline, fixedHz: 60, maxStepsPerFrame: 3);

        long ticksPerSec = 10_000;
        var raw = new InputSample(0f, 0f, 0f, 0f, false, false, 0);

        // Initialize with non-zero timestamp
        engine.Step(in raw, [], hostNowTicks: 1000, ticksPerSecond: ticksPerSec);
        Assert.Equal(0, engine.OverrunCount);

        // 200ms stall → 12 ticks of debt, cap=3 → overrun
        engine.Step(in raw, [], hostNowTicks: 3000, ticksPerSecond: ticksPerSec);

        Assert.Equal(1, engine.OverrunCount);
    }

    [Fact]
    public void NormalOperation_NoOverrun()
    {
        var pipeline = new TransformPipeline();
        var engine = new DeterministicPipeline(pipeline, fixedHz: 60, maxStepsPerFrame: 5);

        long ticksPerSec = 10_000;
        var raw = new InputSample(0f, 0f, 0f, 0f, false, false, 0);

        // Initialize with non-zero timestamp
        engine.Step(in raw, [], hostNowTicks: 1000, ticksPerSecond: ticksPerSec);

        // Feed 10 normal frames at ~16.7ms cadence (167 ticks at 10k/s)
        for (int i = 1; i <= 10; i++)
        {
            engine.Step(in raw, [], hostNowTicks: 1000 + i * 167, ticksPerSecond: ticksPerSec);
        }

        Assert.Equal(0, engine.OverrunCount);
    }
}
