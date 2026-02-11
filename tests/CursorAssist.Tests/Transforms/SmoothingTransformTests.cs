using CursorAssist.Canon.Schemas;
using CursorAssist.Engine.Core;
using CursorAssist.Engine.Transforms;
using Xunit;

namespace CursorAssist.Tests.Transforms;

public class SmoothingTransformTests
{
    /// <summary>
    /// Helper: create an AssistiveConfig with DSP-grounded defaults.
    /// Default minAlpha=0.25 → fc≈2.4Hz, maxAlpha=0.9 → fc≈8.6Hz.
    /// vLow=0.5 vpx/tick (tremor ceiling), vHigh=8.0 vpx/tick (intentional floor).
    /// </summary>
    private static AssistiveConfig MakeConfig(
        float strength = 0.8f,
        float minAlpha = 0.25f,
        float maxAlpha = 0.9f,
        float vLow = 0.5f,
        float vHigh = 8f) => new()
    {
        SourceProfileId = "t",
        SmoothingStrength = strength,
        SmoothingMinAlpha = minAlpha,
        SmoothingMaxAlpha = maxAlpha,
        SmoothingVelocityLow = vLow,
        SmoothingVelocityHigh = vHigh
    };

    [Fact]
    public void NoConfig_PassesThrough()
    {
        var transform = new SmoothingTransform();
        var input = new InputSample(100f, 200f, 0f, 0f, false, false, 0);
        var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f };

        var result = transform.Apply(in input, ctx);
        Assert.Equal(100f, result.X);
        Assert.Equal(200f, result.Y);
    }

    [Fact]
    public void ZeroStrength_PassesThrough()
    {
        var transform = new SmoothingTransform();
        var config = MakeConfig(strength: 0f);
        var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = config };

        var input = new InputSample(100f, 200f, 0f, 0f, false, false, 0);
        var result = transform.Apply(in input, ctx);
        Assert.Equal(100f, result.X);
    }

    [Fact]
    public void HighStrength_BelowVelocityLow_LocksToMinAlpha()
    {
        // Delta=0.3 is below vLow=0.5, so alpha should be exactly minAlpha
        var transform = new SmoothingTransform();
        var config = MakeConfig(strength: 1f, minAlpha: 0.25f, maxAlpha: 0.9f, vLow: 0.5f, vHigh: 8f);

        var init = new InputSample(100f, 100f, 0f, 0f, false, false, 0);
        var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = config };
        transform.Apply(in init, ctx);

        // Tiny jitter: 0.3px (below vLow → locked to minAlpha=0.25)
        var jitter = new InputSample(100.3f, 100f, 0.3f, 0f, false, false, 1);
        ctx = new TransformContext { Tick = 1, Dt = 1f / 60f, Config = config };
        var result = transform.Apply(in jitter, ctx);

        // Expected: 100 + 0.25 * 0.3 = 100.075
        Assert.Equal(100.075f, result.X, 3);
    }

    [Fact]
    public void HighStrength_AboveVelocityHigh_LocksToMaxAlpha()
    {
        // Delta=15 is above vHigh=8, so alpha should be exactly maxAlpha
        var transform = new SmoothingTransform();
        var config = MakeConfig(strength: 1f, minAlpha: 0.25f, maxAlpha: 0.9f, vLow: 0.5f, vHigh: 8f);

        var init = new InputSample(100f, 100f, 0f, 0f, false, false, 0);
        var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = config };
        transform.Apply(in init, ctx);

        // Large intentional movement: 15px (above vHigh → locked to maxAlpha=0.9)
        var move = new InputSample(115f, 100f, 15f, 0f, false, false, 1);
        ctx = new TransformContext { Tick = 1, Dt = 1f / 60f, Config = config };
        var result = transform.Apply(in move, ctx);

        // Expected: 100 + 0.9 * 15 = 113.5
        Assert.Equal(113.5f, result.X, 1);
    }

    [Fact]
    public void HighStrength_BetweenBreakpoints_Interpolates()
    {
        // Delta=4 is between vLow=0.5 and vHigh=8, so alpha is SmoothStep-interpolated
        var transform = new SmoothingTransform();
        var config = MakeConfig(strength: 1f, minAlpha: 0.25f, maxAlpha: 0.9f, vLow: 0.5f, vHigh: 8f);

        var init = new InputSample(100f, 100f, 0f, 0f, false, false, 0);
        var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = config };
        transform.Apply(in init, ctx);

        // Medium movement: 4px (between breakpoints → interpolated alpha)
        var move = new InputSample(104f, 100f, 4f, 0f, false, false, 1);
        ctx = new TransformContext { Tick = 1, Dt = 1f / 60f, Config = config };
        var result = transform.Apply(in move, ctx);

        // Alpha should be between minAlpha and maxAlpha, so result between extremes
        float minExpected = 100f + 0.25f * 4f; // 101.0
        float maxExpected = 100f + 0.9f * 4f;  // 103.6
        Assert.True(result.X > minExpected, $"Result {result.X} should be > {minExpected} (not stuck at min)");
        Assert.True(result.X < maxExpected, $"Result {result.X} should be < {maxExpected} (not at max)");
    }

    [Fact]
    public void VelocityAdaptive_MoreSmoothingAtLowVelocity_LessAtHigh()
    {
        // Core behavioral proof: low velocity tracks less, high velocity tracks more
        var config = MakeConfig(strength: 1f, minAlpha: 0.20f, maxAlpha: 0.95f, vLow: 0.5f, vHigh: 10f);

        // Run 1: Low velocity (delta=0.3, below vLow)
        var t1 = new SmoothingTransform();
        var ctx1 = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = config };
        t1.Apply(new InputSample(100f, 100f, 0f, 0f, false, false, 0), ctx1);

        ctx1 = new TransformContext { Tick = 1, Dt = 1f / 60f, Config = config };
        var lowVelResult = t1.Apply(new InputSample(100.3f, 100f, 0.3f, 0f, false, false, 1), ctx1);
        float lowVelFraction = (lowVelResult.X - 100f) / 0.3f;

        // Run 2: High velocity (delta=20, above vHigh)
        var t2 = new SmoothingTransform();
        var ctx2 = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = config };
        t2.Apply(new InputSample(100f, 100f, 0f, 0f, false, false, 0), ctx2);

        ctx2 = new TransformContext { Tick = 1, Dt = 1f / 60f, Config = config };
        var highVelResult = t2.Apply(new InputSample(120f, 100f, 20f, 0f, false, false, 1), ctx2);
        float highVelFraction = (highVelResult.X - 100f) / 20f;

        Assert.True(highVelFraction > lowVelFraction,
            $"High velocity should track more ({highVelFraction:F3}) than low velocity ({lowVelFraction:F3})");
    }

    [Fact]
    public void Deterministic_SameInputsSameOutput()
    {
        var t1 = new SmoothingTransform();
        var t2 = new SmoothingTransform();
        var config = MakeConfig(strength: 0.5f);

        var samples = new[]
        {
            new InputSample(100f, 100f, 0f, 0f, false, false, 0),
            new InputSample(110f, 105f, 10f, 5f, false, false, 1),
            new InputSample(105f, 98f, -5f, -7f, false, false, 2),
        };

        var results1 = new InputSample[samples.Length];
        var results2 = new InputSample[samples.Length];

        for (int i = 0; i < samples.Length; i++)
        {
            var ctx = new TransformContext { Tick = i, Dt = 1f / 60f, Config = config };
            results1[i] = t1.Apply(in samples[i], ctx);
            results2[i] = t2.Apply(in samples[i], ctx);
        }

        for (int i = 0; i < samples.Length; i++)
        {
            Assert.Equal(results1[i].X, results2[i].X);
            Assert.Equal(results1[i].Y, results2[i].Y);
        }
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var transform = new SmoothingTransform();
        var config = MakeConfig(strength: 0.9f);

        var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = config };
        transform.Apply(new InputSample(100f, 100f, 0f, 0f, false, false, 0), ctx);
        transform.Apply(new InputSample(200f, 200f, 100f, 100f, false, false, 1), ctx);

        transform.Reset();

        var input = new InputSample(500f, 500f, 0f, 0f, false, false, 0);
        var result = transform.Apply(in input, ctx);
        Assert.Equal(500f, result.X);
        Assert.Equal(500f, result.Y);
    }

    [Fact]
    public void SmoothStep_TransitionCurveIsMonotonic()
    {
        // Verify monotonic increase: higher velocity → higher alpha → larger fraction tracked
        var config = MakeConfig(strength: 1f, minAlpha: 0.20f, maxAlpha: 0.95f, vLow: 0.5f, vHigh: 10f);

        float prevFraction = 0f;

        for (int v = 1; v <= 20; v += 2)
        {
            var t = new SmoothingTransform();
            var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = config };
            t.Apply(new InputSample(100f, 100f, 0f, 0f, false, false, 0), ctx);

            float dx = (float)v;
            ctx = new TransformContext { Tick = 1, Dt = 1f / 60f, Config = config };
            var result = t.Apply(new InputSample(100f + dx, 100f, dx, 0f, false, false, 1), ctx);

            float fraction = (result.X - 100f) / dx;

            Assert.True(fraction >= prevFraction - 0.001f,
                $"Velocity {v}: fraction {fraction:F4} should be >= previous {prevFraction:F4}");

            prevFraction = fraction;
        }
    }

    [Fact]
    public void LowStrength_BiasesAlphaUpward()
    {
        var config1 = MakeConfig(strength: 1f, minAlpha: 0.20f, maxAlpha: 0.9f, vLow: 0.5f, vHigh: 8f);
        var config05 = MakeConfig(strength: 0.5f, minAlpha: 0.20f, maxAlpha: 0.9f, vLow: 0.5f, vHigh: 8f);

        var t1 = new SmoothingTransform();
        var t05 = new SmoothingTransform();

        var ctx1 = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = config1 };
        var ctx05 = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = config05 };
        t1.Apply(new InputSample(100f, 100f, 0f, 0f, false, false, 0), ctx1);
        t05.Apply(new InputSample(100f, 100f, 0f, 0f, false, false, 0), ctx05);

        // Low velocity jitter (delta=0.3, below vLow)
        ctx1 = new TransformContext { Tick = 1, Dt = 1f / 60f, Config = config1 };
        ctx05 = new TransformContext { Tick = 1, Dt = 1f / 60f, Config = config05 };
        var r1 = t1.Apply(new InputSample(102f, 100f, 2f, 0f, false, false, 1), ctx1);
        var r05 = t05.Apply(new InputSample(102f, 100f, 2f, 0f, false, false, 1), ctx05);

        // strength=0.5 should track MORE (less smoothing overall)
        Assert.True(r05.X > r1.X,
            $"Strength 0.5 ({r05.X:F3}) should track more than strength 1.0 ({r1.X:F3})");
    }

    [Fact]
    public void CutoffFrequency_MinAlpha025_ApproxTwoPointFourHz()
    {
        // DSP sanity check: at minAlpha=0.25, fc ≈ 0.25*60/(2π) ≈ 2.4 Hz
        // A 2.4 Hz sine at 60 Hz has ~25 samples/cycle
        // At -3dB, output amplitude should be ~0.707 of input amplitude
        //
        // We test indirectly: run a low-frequency "step" and a high-frequency
        // oscillation, verify the oscillation is suppressed more
        var config = MakeConfig(strength: 1f, minAlpha: 0.25f, maxAlpha: 0.25f, vLow: 0f, vHigh: 100f);
        // Force constant alpha=0.25 by making vHigh huge and vLow=0

        var transform = new SmoothingTransform();
        var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = config };
        transform.Apply(new InputSample(0f, 0f, 0f, 0f, false, false, 0), ctx);

        // Simulate 8 Hz oscillation for 60 ticks (1 second)
        // 8 Hz at 60 Hz = 7.5 samples/cycle, amplitude = 5 vpx
        float maxOutput = 0f;

        for (int i = 1; i <= 60; i++)
        {
            float phase = 2f * MathF.PI * 8f * i / 60f;
            float x = 5f * MathF.Sin(phase);
            float dx = x - (5f * MathF.Sin(2f * MathF.PI * 8f * (i - 1) / 60f));

            ctx = new TransformContext { Tick = i, Dt = 1f / 60f, Config = config };
            var result = transform.Apply(new InputSample(x, 0f, dx, 0f, false, false, i), ctx);

            if (i > 30) // Skip transient
            {
                float absOutput = MathF.Abs(result.X);
                if (absOutput > maxOutput) maxOutput = absOutput;
            }
        }

        // 8 Hz tremor with α=0.25 should be attenuated well below input amplitude (5 vpx)
        Assert.True(maxOutput < 3.5f,
            $"8 Hz oscillation should be attenuated: peak output {maxOutput:F2} vpx should be < 3.5 (input amplitude 5.0)");
    }
}
