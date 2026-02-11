using CursorAssist.Canon.Schemas;
using CursorAssist.Engine.Core;
using CursorAssist.Engine.Transforms;
using CursorAssist.Runtime.Core;
using CursorAssist.Tests.Helpers;
using Xunit;

namespace CursorAssist.Tests.Stability;

/// <summary>
/// Long-duration and numerical stability tests for the full assistive pipeline.
/// Proves the engine cannot produce NaN, drift, oscillation growth, or overshoot
/// over extended simulated sessions.
/// </summary>
public class StabilityTests
{
    [Fact]
    public void LongDuration_36000Ticks_NoNaNNoDriftNoOscillationGrowth()
    {
        // 36,000 ticks = 10 minutes at 60 Hz
        const int totalTicks = 36_000;
        const int windowSize = 6_000;
        const int windowCount = totalTicks / windowSize;

        var pipeline = TestStreamGenerator.BuildFullPipeline();
        var engine = new DeterministicPipeline(pipeline);
        var config = TestStreamGenerator.MakeRealisticConfig();
        var events = TestStreamGenerator.GenerateDeterministicStream(totalTicks, seed: 12345);

        float curX = 0f, curY = 0f;

        // Track per-window min/max for oscillation growth detection
        var windowMinX = new float[windowCount];
        var windowMaxX = new float[windowCount];
        for (int w = 0; w < windowCount; w++)
        {
            windowMinX[w] = float.MaxValue;
            windowMaxX[w] = float.MinValue;
        }

        for (int i = 0; i < totalTicks; i++)
        {
            var evt = events[i];
            curX += evt.Dx;
            curY += evt.Dy;

            var input = new InputSample(curX, curY, evt.Dx, evt.Dy,
                evt.PrimaryDown, evt.SecondaryDown, i);

            var ctx = new TransformContext
            {
                Tick = i,
                Dt = 1f / 60f,
                Config = config
            };

            var result = engine.FixedStep(in input, ctx);
            float outX = result.FinalCursor.X;
            float outY = result.FinalCursor.Y;

            // Per-tick: no NaN, no Infinity, within generous bounds
            Assert.False(float.IsNaN(outX), $"NaN at tick {i} on X");
            Assert.False(float.IsNaN(outY), $"NaN at tick {i} on Y");
            Assert.False(float.IsInfinity(outX), $"Infinity at tick {i} on X");
            Assert.False(float.IsInfinity(outY), $"Infinity at tick {i} on Y");
            Assert.InRange(outX, -100_000f, 100_000f);
            Assert.InRange(outY, -100_000f, 100_000f);

            // Track window min/max
            int window = i / windowSize;
            if (outX < windowMinX[window]) windowMinX[window] = outX;
            if (outX > windowMaxX[window]) windowMaxX[window] = outX;
        }

        // End position: random walk with σ≈2.5 vpx/tick, √36000≈190, expected |pos|≈475 vpx.
        // 5000 vpx bound is >10σ.
        float finalX = curX; // raw position (transforms may shift it)
        Assert.InRange(curX, -5000f, 5000f);
        Assert.InRange(curY, -5000f, 5000f);

        // Oscillation growth: last window amplitude ≤ 2× first window + slack
        float firstAmplitude = windowMaxX[0] - windowMinX[0];
        float lastAmplitude = windowMaxX[windowCount - 1] - windowMinX[windowCount - 1];
        Assert.True(lastAmplitude <= firstAmplitude * 2.0f + 10f,
            $"Oscillation growth detected: first window amplitude={firstAmplitude:F2}, " +
            $"last window amplitude={lastAmplitude:F2}");
    }

    [Fact]
    public void ZeroInput_10000Ticks_NoDrift()
    {
        const int totalTicks = 10_000;
        const float initX = 500f;
        const float initY = 500f;

        var pipeline = TestStreamGenerator.BuildFullPipeline();
        var engine = new DeterministicPipeline(pipeline);
        var config = TestStreamGenerator.MakeRealisticConfig();

        for (int i = 0; i < totalTicks; i++)
        {
            var input = new InputSample(initX, initY, 0f, 0f, false, false, i);

            var ctx = new TransformContext
            {
                Tick = i,
                Dt = 1f / 60f,
                Config = config
            };

            var result = engine.FixedStep(in input, ctx);
            float outX = result.FinalCursor.X;
            float outY = result.FinalCursor.Y;

            Assert.True(MathF.Abs(outX - initX) < 0.01f,
                $"X drifted to {outX} at tick {i} (expected {initX})");
            Assert.True(MathF.Abs(outY - initY) < 0.01f,
                $"Y drifted to {outY} at tick {i} (expected {initY})");
        }
    }

    /// <summary>
    /// Sweep sinusoidal tremor at 4–10 Hz through the full pipeline.
    /// Verifies output amplitude is strictly less than input amplitude
    /// (tremor suppressed). The smoothing filter's −3 dB cutoff is at
    /// ~2.4 Hz (minAlpha=0.25) so the full 4–10 Hz band is attenuated.
    /// 12 Hz is excluded: at high velocity the smoothing alpha approaches
    /// maxAlpha (near pass-through), and phase compensation can amplify
    /// near-Nyquist oscillations — that's tested separately for stability.
    /// </summary>
    [Theory]
    [InlineData(4f)]
    [InlineData(6f)]
    [InlineData(8f)]
    [InlineData(10f)]
    public void TremorSineSuppression_FullPipeline(float frequencyHz)
    {
        const int totalTicks = 300; // 5 seconds
        const float inputAmplitude = 3.0f;
        const int warmupTicks = 60; // 1 second transient warmup

        var pipeline = TestStreamGenerator.BuildFullPipeline();
        var engine = new DeterministicPipeline(pipeline);
        var config = TestStreamGenerator.MakeRealisticConfig();
        var events = TestStreamGenerator.GenerateSineStream(totalTicks, frequencyHz, inputAmplitude);

        float curX = 0f;
        float maxOutputX = float.MinValue;
        float minOutputX = float.MaxValue;

        for (int i = 0; i < totalTicks; i++)
        {
            var evt = events[i];
            curX += evt.Dx;

            var input = new InputSample(curX, 0f, evt.Dx, 0f,
                false, false, i);

            var ctx = new TransformContext
            {
                Tick = i,
                Dt = 1f / 60f,
                Config = config
            };

            var result = engine.FixedStep(in input, ctx);
            float outX = result.FinalCursor.X;

            Assert.False(float.IsNaN(outX), $"NaN at tick {i}");

            // Track output amplitude after warmup
            if (i >= warmupTicks)
            {
                if (outX > maxOutputX) maxOutputX = outX;
                if (outX < minOutputX) minOutputX = outX;
            }
        }

        float outputAmplitude = (maxOutputX - minOutputX) / 2f;

        // Output amplitude must be strictly less than input (tremor suppressed)
        Assert.True(outputAmplitude < inputAmplitude,
            $"At {frequencyHz} Hz: output amplitude {outputAmplitude:F3} should be < input {inputAmplitude}");

        // Output should not be completely zeroed (transforms are moderate, not aggressive)
        Assert.True(outputAmplitude > 0f,
            $"At {frequencyHz} Hz: output amplitude should be > 0");
    }

    /// <summary>
    /// At 12 Hz, phase compensation can amplify high-frequency oscillations
    /// (feed-forward velocity projection adds energy near Nyquist). This test
    /// verifies stability (no NaN, bounded output) rather than suppression.
    /// </summary>
    [Fact]
    public void TremorSine_12Hz_StableNotAmplifiedUnboundedly()
    {
        const int totalTicks = 300;
        const float inputAmplitude = 3.0f;

        var pipeline = TestStreamGenerator.BuildFullPipeline();
        var engine = new DeterministicPipeline(pipeline);
        var config = TestStreamGenerator.MakeRealisticConfig();
        var events = TestStreamGenerator.GenerateSineStream(totalTicks, 12f, inputAmplitude);

        float curX = 0f;
        float maxOutputX = float.MinValue;
        float minOutputX = float.MaxValue;

        for (int i = 0; i < totalTicks; i++)
        {
            var evt = events[i];
            curX += evt.Dx;

            var input = new InputSample(curX, 0f, evt.Dx, 0f,
                false, false, i);

            var ctx = new TransformContext
            {
                Tick = i,
                Dt = 1f / 60f,
                Config = config
            };

            var result = engine.FixedStep(in input, ctx);
            float outX = result.FinalCursor.X;

            Assert.False(float.IsNaN(outX), $"NaN at tick {i}");
            Assert.False(float.IsInfinity(outX), $"Infinity at tick {i}");

            if (i >= 60) // After warmup
            {
                if (outX > maxOutputX) maxOutputX = outX;
                if (outX < minOutputX) minOutputX = outX;
            }
        }

        float outputAmplitude = (maxOutputX - minOutputX) / 2f;

        // At 12 Hz with phase comp, output may be amplified but must be bounded.
        // Input amplitude is 3.0 vpx; output should not exceed 20 vpx (generous bound).
        Assert.True(outputAmplitude < 20f,
            $"At 12 Hz: output amplitude {outputAmplitude:F3} should be bounded (< 20 vpx)");
    }

    [Fact]
    public void CompensationOvershoot_ConstantVelocityThenStop_NoOscillatoryTail()
    {
        const int moveTicks = 60;
        const int stopTicks = 120;
        const float dxPerTick = 5f;

        // Pipeline: deadzone + smoothing + phase comp (the overshoot-relevant chain)
        var pipeline = new TransformPipeline()
            .Add(new SoftDeadzoneTransform())
            .Add(new SmoothingTransform())
            .Add(new PhaseCompensationTransform());

        var engine = new DeterministicPipeline(pipeline);

        var config = new AssistiveConfig
        {
            SourceProfileId = "overshoot-test",
            SmoothingStrength = 0.8f,
            SmoothingMinAlpha = 0.25f,
            SmoothingMaxAlpha = 0.90f,
            SmoothingVelocityLow = 0.5f,
            SmoothingVelocityHigh = 8f,
            DeadzoneRadiusVpx = 1.0f,
            PhaseCompensationGainS = 0.015f // Intentionally aggressive
        };

        var events = TestStreamGenerator.GenerateConstantVelocityThenStop(moveTicks, stopTicks, dxPerTick);
        var outputPositions = new float[moveTicks + stopTicks];
        float curX = 0f;

        for (int i = 0; i < events.Count; i++)
        {
            var evt = events[i];
            curX += evt.Dx;

            var input = new InputSample(curX, 0f, evt.Dx, 0f,
                false, false, i);

            var ctx = new TransformContext
            {
                Tick = i,
                Dt = 1f / 60f,
                Config = config
            };

            var result = engine.FixedStep(in input, ctx);
            outputPositions[i] = result.FinalCursor.X;
        }

        // Settled position = output at the very end (tick 179)
        float settledPosition = outputPositions[moveTicks + stopTicks - 1];

        // Measure max deviation from settled position during ticks 90–180 (30+ ticks after stop)
        float maxDeviation = 0f;
        for (int i = moveTicks + 30; i < moveTicks + stopTicks; i++)
        {
            float dev = MathF.Abs(outputPositions[i] - settledPosition);
            if (dev > maxDeviation) maxDeviation = dev;
        }

        Assert.True(maxDeviation < 0.5f,
            $"Overshoot window: max deviation {maxDeviation:F4} from settled {settledPosition:F2} " +
            $"should be < 0.5 vpx");

        // Count sign changes in delta after tick 90 (no oscillatory tail)
        int signChanges = 0;
        for (int i = moveTicks + 31; i < moveTicks + stopTicks; i++)
        {
            float prevDelta = outputPositions[i - 1] - outputPositions[i - 2];
            float currDelta = outputPositions[i] - outputPositions[i - 1];

            // Only count meaningful sign changes (ignore near-zero jitter)
            if (MathF.Abs(prevDelta) > 1e-5f && MathF.Abs(currDelta) > 1e-5f)
            {
                if ((prevDelta > 0 && currDelta < 0) || (prevDelta < 0 && currDelta > 0))
                {
                    signChanges++;
                }
            }
        }

        Assert.True(signChanges <= 3,
            $"Oscillatory tail: {signChanges} sign changes in delta after settling (should be ≤ 3)");

        // Final 30 ticks should all be within 0.1 vpx of settled
        for (int i = moveTicks + stopTicks - 30; i < moveTicks + stopTicks; i++)
        {
            float dev = MathF.Abs(outputPositions[i] - settledPosition);
            Assert.True(dev < 0.1f,
                $"Tick {i}: deviation {dev:F6} from settled should be < 0.1 vpx");
        }
    }

    [Fact]
    public void FullPipelineDeterministicReplay_AllFiveTransforms_StableHash()
    {
        var config = TestStreamGenerator.MakeRealisticConfig();
        var events = TestStreamGenerator.GenerateDeterministicStream(500, seed: 77);

        // Run 1
        var pipeline1 = TestStreamGenerator.BuildFullPipeline();
        var engine1 = new EngineThread(pipeline1);
        engine1.UpdateConfig(config);
        ulong hash1 = engine1.ReplayStream(events);

        // Run 2 (fresh instances)
        var pipeline2 = TestStreamGenerator.BuildFullPipeline();
        var engine2 = new EngineThread(pipeline2);
        engine2.UpdateConfig(config);
        ulong hash2 = engine2.ReplayStream(events);

        Assert.Equal(hash1, hash2);
        Assert.NotEqual(0UL, hash1);

        // Sanity: different seed → different hash
        var events3 = TestStreamGenerator.GenerateDeterministicStream(500, seed: 78);
        var pipeline3 = TestStreamGenerator.BuildFullPipeline();
        var engine3 = new EngineThread(pipeline3);
        engine3.UpdateConfig(config);
        ulong hash3 = engine3.ReplayStream(events3);

        Assert.NotEqual(hash1, hash3);
    }
}
