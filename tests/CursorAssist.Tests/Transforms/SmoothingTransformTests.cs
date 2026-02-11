using CursorAssist.Canon.Schemas;
using CursorAssist.Engine.Core;
using CursorAssist.Engine.Transforms;
using Xunit;

namespace CursorAssist.Tests.Transforms;

public class SmoothingTransformTests
{
    private static AssistiveConfig MakeConfig(
        float strength = 0.8f,
        float minAlpha = 0.08f,
        float maxAlpha = 0.9f,
        float vMax = 8f) => new()
    {
        SourceProfileId = "t",
        SmoothingStrength = strength,
        SmoothingMinAlpha = minAlpha,
        SmoothingMaxAlpha = maxAlpha,
        SmoothingVelocityMax = vMax
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
    public void HighStrength_SmallDelta_StronglySmoothsJitter()
    {
        // Small delta (low velocity) should get heavy smoothing
        var transform = new SmoothingTransform();
        var config = MakeConfig(strength: 1f, minAlpha: 0.05f, maxAlpha: 0.9f, vMax: 8f);

        // Initialize at 100,100
        var init = new InputSample(100f, 100f, 0f, 0f, false, false, 0);
        var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = config };
        transform.Apply(in init, ctx);

        // Small jitter: 2px jump (well below vMax → near minAlpha → heavy smoothing)
        var jitter = new InputSample(102f, 100f, 2f, 0f, false, false, 1);
        ctx = new TransformContext { Tick = 1, Dt = 1f / 60f, Config = config };
        var result = transform.Apply(in jitter, ctx);

        // With very low alpha (~0.05), output should barely move
        Assert.True(result.X > 100f, "Should move slightly toward jitter");
        Assert.True(result.X < 101f, "Should barely follow small jitter — heavy smoothing");
    }

    [Fact]
    public void HighStrength_LargeDelta_PreservesResponsiveness()
    {
        // Large delta (high velocity) should get light smoothing
        var transform = new SmoothingTransform();
        var config = MakeConfig(strength: 1f, minAlpha: 0.05f, maxAlpha: 0.9f, vMax: 8f);

        // Initialize at 100,100
        var init = new InputSample(100f, 100f, 0f, 0f, false, false, 0);
        var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = config };
        transform.Apply(in init, ctx);

        // Large intentional movement: 50px jump (well above vMax → near maxAlpha)
        var move = new InputSample(150f, 100f, 50f, 0f, false, false, 1);
        ctx = new TransformContext { Tick = 1, Dt = 1f / 60f, Config = config };
        var result = transform.Apply(in move, ctx);

        // With high alpha (~0.9), output should closely follow
        Assert.True(result.X > 140f, "Should mostly follow large intentional movement");
        Assert.True(result.X <= 150f, "Should not overshoot raw input");
    }

    [Fact]
    public void VelocityAdaptive_MoreSmoothingAtLowVelocity_LessAtHigh()
    {
        // This is the core test: same displacement, but different velocities
        // should produce different smoothing amounts
        var config = MakeConfig(strength: 1f, minAlpha: 0.05f, maxAlpha: 0.95f, vMax: 10f);

        // Run 1: Low velocity (small delta)
        var t1 = new SmoothingTransform();
        var ctx1 = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = config };
        t1.Apply(new InputSample(100f, 100f, 0f, 0f, false, false, 0), ctx1);

        ctx1 = new TransformContext { Tick = 1, Dt = 1f / 60f, Config = config };
        var lowVelResult = t1.Apply(new InputSample(101f, 100f, 1f, 0f, false, false, 1), ctx1);
        float lowVelDisplacement = lowVelResult.X - 100f; // How far it moved

        // Run 2: High velocity (large delta) — then same 1px error from different baseline
        var t2 = new SmoothingTransform();
        var ctx2 = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = config };
        t2.Apply(new InputSample(100f, 100f, 0f, 0f, false, false, 0), ctx2);

        ctx2 = new TransformContext { Tick = 1, Dt = 1f / 60f, Config = config };
        var highVelResult = t2.Apply(new InputSample(120f, 100f, 20f, 0f, false, false, 1), ctx2);
        float highVelFraction = (highVelResult.X - 100f) / 20f; // Fraction of delta tracked

        float lowVelFraction = lowVelDisplacement / 1f;

        // High velocity should track a LARGER fraction than low velocity
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

        // Build up state
        var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = config };
        transform.Apply(new InputSample(100f, 100f, 0f, 0f, false, false, 0), ctx);
        transform.Apply(new InputSample(200f, 200f, 100f, 100f, false, false, 1), ctx);

        transform.Reset();

        // After reset, should treat next input as initialization
        var input = new InputSample(500f, 500f, 0f, 0f, false, false, 0);
        var result = transform.Apply(in input, ctx);
        Assert.Equal(500f, result.X);
        Assert.Equal(500f, result.Y);
    }

    [Fact]
    public void SmoothStep_TransitionCurveIsSmooth()
    {
        // Verify the smoothstep velocity→alpha mapping produces a monotonic,
        // smooth transition by feeding increasing velocities
        var transform = new SmoothingTransform();
        var config = MakeConfig(strength: 1f, minAlpha: 0.05f, maxAlpha: 0.95f, vMax: 10f);

        float prevFraction = 0f;

        for (int v = 0; v <= 20; v += 2)
        {
            var t = new SmoothingTransform();
            var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = config };
            t.Apply(new InputSample(100f, 100f, 0f, 0f, false, false, 0), ctx);

            float dx = (float)v;
            ctx = new TransformContext { Tick = 1, Dt = 1f / 60f, Config = config };
            var result = t.Apply(new InputSample(100f + dx, 100f, dx, 0f, false, false, 1), ctx);

            float fraction = dx > 0f ? (result.X - 100f) / dx : 0f;

            if (v > 0)
            {
                // Each higher velocity should track at least as much of the delta
                Assert.True(fraction >= prevFraction - 0.001f,
                    $"Velocity {v}: fraction {fraction:F4} should be >= previous {prevFraction:F4}");
            }

            prevFraction = fraction;
        }
    }

    [Fact]
    public void LowStrength_BiasesAlphaUpward()
    {
        // Lower SmoothingStrength should reduce the effect of the velocity-adaptive
        // filter (bias alpha toward 1.0 = less smoothing overall)
        var config1 = MakeConfig(strength: 1f, minAlpha: 0.05f, maxAlpha: 0.9f, vMax: 8f);
        var config05 = MakeConfig(strength: 0.5f, minAlpha: 0.05f, maxAlpha: 0.9f, vMax: 8f);

        // Low velocity, small jitter
        var t1 = new SmoothingTransform();
        var t05 = new SmoothingTransform();

        var ctx1 = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = config1 };
        var ctx05 = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = config05 };
        t1.Apply(new InputSample(100f, 100f, 0f, 0f, false, false, 0), ctx1);
        t05.Apply(new InputSample(100f, 100f, 0f, 0f, false, false, 0), ctx05);

        ctx1 = new TransformContext { Tick = 1, Dt = 1f / 60f, Config = config1 };
        ctx05 = new TransformContext { Tick = 1, Dt = 1f / 60f, Config = config05 };
        var r1 = t1.Apply(new InputSample(102f, 100f, 2f, 0f, false, false, 1), ctx1);
        var r05 = t05.Apply(new InputSample(102f, 100f, 2f, 0f, false, false, 1), ctx05);

        // strength=0.5 should track MORE of the delta than strength=1.0
        // (less smoothing overall)
        Assert.True(r05.X > r1.X,
            $"Strength 0.5 ({r05.X:F3}) should track more than strength 1.0 ({r1.X:F3})");
    }
}
