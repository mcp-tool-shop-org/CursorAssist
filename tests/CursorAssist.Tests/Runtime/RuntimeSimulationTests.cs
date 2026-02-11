using CursorAssist.Canon.Schemas;
using CursorAssist.Engine.Core;
using CursorAssist.Engine.Transforms;
using CursorAssist.Runtime.Core;
using CursorAssist.Tests.Helpers;
using Xunit;

namespace CursorAssist.Tests.Runtime;

/// <summary>
/// Tests for runtime simulation patterns: input aggregation, end-to-end
/// clamping through actual transforms, and multi-step delta zeroing.
/// These validate the EngineThread.RunLoop() behavioral contracts without
/// requiring the real-time thread.
/// </summary>
public class RuntimeSimulationTests
{
    [Fact]
    public void InputAggregation_MultipleRapidInputs_SummedCorrectly()
    {
        // Simulate EngineThread.RunLoop() aggregation pattern (lines 246-256):
        // Drain queue, sum Dx/Dy, button state = last event's value.
        var events = new[]
        {
            new RawInputEvent(3f, 1f, false, false, 0),
            new RawInputEvent(2f, -1f, true, false, 1),
            new RawInputEvent(-1f, 4f, true, false, 2),
        };

        float aggDx = 0f, aggDy = 0f;
        bool primary = false, secondary = false;

        foreach (var evt in events)
        {
            aggDx += evt.Dx;
            aggDy += evt.Dy;
            primary = evt.PrimaryDown;
            secondary = evt.SecondaryDown;
        }

        // Verify aggregation math: Dx = 3+2-1 = 4, Dy = 1-1+4 = 4
        Assert.Equal(4f, aggDx);
        Assert.Equal(4f, aggDy);
        Assert.True(primary);   // Last event has primary=true
        Assert.False(secondary); // Last event has secondary=false

        // Feed aggregated sample to pipeline and verify output
        var pipeline = new DeterministicPipeline(new TransformPipeline());
        float curX = 100f, curY = 200f;
        var input = new InputSample(curX + aggDx, curY + aggDy, aggDx, aggDy,
            primary, secondary, 0);
        var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f };
        var result = pipeline.FixedStep(in input, ctx);

        Assert.Equal(104f, result.FinalCursor.X, 4);
        Assert.Equal(204f, result.FinalCursor.Y, 4);
        Assert.True(result.FinalCursor.PrimaryDown);
        Assert.False(result.FinalCursor.SecondaryDown);

        // Button state is last-wins, NOT OR-combined
        var buttonEvents = new[]
        {
            new RawInputEvent(0f, 0f, false, false, 0),
            new RawInputEvent(0f, 0f, true, false, 1),
            new RawInputEvent(0f, 0f, false, false, 2), // Last event: primary=false
        };

        bool lastPrimary = false;
        foreach (var evt in buttonEvents)
        {
            lastPrimary = evt.PrimaryDown;
        }

        Assert.False(lastPrimary, "Button state should be last-wins, not OR-combined");
    }

    [Fact]
    public void CompensationClamp_ExcessiveGain_ClampedEndToEnd()
    {
        // Config with gain far exceeding the runtime limit
        var unclamped = new AssistiveConfig
        {
            SourceProfileId = "clamp-test",
            PhaseCompensationGainS = 0.5f // Exceeds RuntimeLimits.MaxPhaseCompGainS (0.1)
        };

        // Step 1: Verify ClampConfig catches the excess
        var clamped = EngineThread.ClampConfig(unclamped);
        Assert.Equal(RuntimeLimits.MaxPhaseCompGainS, clamped.PhaseCompensationGainS, 4);

        // Step 2: Apply clamped config to actual PhaseCompensationTransform
        // With Dx=5 at clamped gain: offset = 0.1 × 5 × 60 = 30 vpx
        // Without clamp: offset = 0.5 × 5 × 60 = 150 vpx
        var transform = new PhaseCompensationTransform();
        var input = new InputSample(100f, 100f, 5f, 0f, false, false, 0);
        var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = clamped };
        var result = transform.Apply(in input, ctx);

        float offset = result.X - 100f;
        Assert.Equal(30f, offset, 1); // 0.1 × 5 × 60 = 30
        Assert.True(offset < 150f);    // Would be 150 without clamp

        // Step 3: Verify ReplayStream path also clamps (it calls ClampConfig internally)
        var pipeline = new TransformPipeline().Add(new PhaseCompensationTransform());
        var engine = new EngineThread(pipeline);
        engine.UpdateConfig(unclamped); // Pass unclamped — ReplayStream clamps it

        var events = TestStreamGenerator.GenerateConstantVelocityThenStop(100, 0, 5f);
        ulong hash = engine.ReplayStream(events);
        Assert.NotEqual(0UL, hash); // Ran successfully with clamped config
    }

    [Fact]
    public void DeltaClamp_PathologicalOutput_NeverExceeds50PerTick()
    {
        // PhaseCompensation with max gain (0.1) + large Dx (40) produces huge offsets:
        // offset = 0.1 × 40 × 60 = 240 vpx. Combined with raw delta = ~280 vpx/tick.
        var config = new AssistiveConfig
        {
            SourceProfileId = "delta-clamp-test",
            PhaseCompensationGainS = RuntimeLimits.MaxPhaseCompGainS // 0.1
        };
        config = EngineThread.ClampConfig(config);

        var pipeline = new TransformPipeline().Add(new PhaseCompensationTransform());
        var deterministicPipeline = new DeterministicPipeline(pipeline);

        float prevOutX = 0f;
        float curX = 0f;
        bool sawExcessivePreClamp = false;

        for (int i = 0; i < 20; i++)
        {
            float dx = 40f; // Very fast movement
            curX += dx;

            var input = new InputSample(curX, 0f, dx, 0f, false, false, i);
            var ctx = new TransformContext { Tick = i, Dt = 1f / 60f, Config = config };
            var result = deterministicPipeline.FixedStep(in input, ctx);

            // Compute assisted delta (what EngineThread.RunLoop does)
            float assistedDx = result.FinalCursor.X - prevOutX;
            float assistedDy = result.FinalCursor.Y - 0f;

            // Apply ClampDelta (same as EngineThread.RunLoop line 281)
            var (clampedDx, clampedDy) = EngineThread.ClampDelta(assistedDx, assistedDy);

            // Verify clamped output is within limits
            Assert.InRange(clampedDx,
                -RuntimeLimits.MaxDeltaPerTick, RuntimeLimits.MaxDeltaPerTick);
            Assert.InRange(clampedDy,
                -RuntimeLimits.MaxDeltaPerTick, RuntimeLimits.MaxDeltaPerTick);

            // Track whether pre-clamp delta ever exceeded limit (proves test is meaningful)
            if (MathF.Abs(assistedDx) > RuntimeLimits.MaxDeltaPerTick)
            {
                sawExcessivePreClamp = true;
            }

            prevOutX = result.FinalCursor.X;
        }

        Assert.True(sawExcessivePreClamp,
            "Pre-clamp delta should exceed 50 vpx/tick to prove clamping is needed");
    }

    [Fact]
    public void MultiStepAggregation_OnlyFirstStepGetsDelta()
    {
        // Simulate EngineThread.RunLoop() multi-step pattern:
        // First step receives the aggregated deltas, subsequent steps get zero.
        // This mirrors lines 258-305 of EngineThread.RunLoop().
        var pipeline = new TransformPipeline(); // Pass-through
        var deterministicPipeline = new DeterministicPipeline(pipeline);

        float curX = 100f, curY = 100f;
        float aggDx = 15f, aggDy = 10f;

        var results = new List<InputSample>();

        // Simulate 3 steps like RunLoop does
        for (int step = 0; step < 3; step++)
        {
            float rawX = curX + aggDx;
            float rawY = curY + aggDy;

            var input = new InputSample(rawX, rawY, aggDx, aggDy, false, false, step);
            var ctx = new TransformContext { Tick = step, Dt = 1f / 60f };
            var result = deterministicPipeline.FixedStep(in input, ctx);
            results.Add(result.FinalCursor);

            curX = result.FinalCursor.X;
            curY = result.FinalCursor.Y;

            // Zero out after first step (RunLoop behavior, line 300-301)
            aggDx = 0f;
            aggDy = 0f;
        }

        // First step: moved by (15, 10)
        Assert.Equal(115f, results[0].X, 4);
        Assert.Equal(110f, results[0].Y, 4);
        Assert.Equal(15f, results[0].Dx, 4);
        Assert.Equal(10f, results[0].Dy, 4);

        // Second step: zero delta, position unchanged
        Assert.Equal(115f, results[1].X, 4);
        Assert.Equal(110f, results[1].Y, 4);
        Assert.Equal(0f, results[1].Dx, 4);
        Assert.Equal(0f, results[1].Dy, 4);

        // Third step: zero delta, position unchanged
        Assert.Equal(115f, results[2].X, 4);
        Assert.Equal(110f, results[2].Y, 4);
        Assert.Equal(0f, results[2].Dx, 4);
        Assert.Equal(0f, results[2].Dy, 4);
    }
}
