using CursorAssist.Canon.Schemas;
using CursorAssist.Engine.Core;
using CursorAssist.Engine.Transforms;
using Xunit;

namespace CursorAssist.Tests.Transforms;

public class SoftDeadzoneTransformTests
{
    private static AssistiveConfig MakeConfig(float deadzoneRadius = 1f) => new()
    {
        SourceProfileId = "t",
        DeadzoneRadiusVpx = deadzoneRadius
    };

    [Fact]
    public void ZeroRadius_PassesThrough()
    {
        var transform = new SoftDeadzoneTransform();
        var config = MakeConfig(deadzoneRadius: 0f);
        var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = config };

        var init = new InputSample(100f, 100f, 0f, 0f, false, false, 0);
        transform.Apply(in init, ctx);

        var input = new InputSample(105f, 103f, 5f, 3f, false, false, 1);
        ctx = new TransformContext { Tick = 1, Dt = 1f / 60f, Config = config };
        var result = transform.Apply(in input, ctx);

        Assert.Equal(105f, result.X, 4);
        Assert.Equal(103f, result.Y, 4);
        Assert.Equal(5f, result.Dx, 4);
        Assert.Equal(3f, result.Dy, 4);
    }

    [Fact]
    public void NoConfig_PassesThrough()
    {
        var transform = new SoftDeadzoneTransform();
        var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f };

        var init = new InputSample(100f, 100f, 0f, 0f, false, false, 0);
        transform.Apply(in init, ctx);

        var input = new InputSample(105f, 103f, 5f, 3f, false, false, 1);
        ctx = new TransformContext { Tick = 1, Dt = 1f / 60f };
        var result = transform.Apply(in input, ctx);

        Assert.Equal(105f, result.X, 4);
        Assert.Equal(103f, result.Y, 4);
    }

    [Fact]
    public void SmallDelta_Suppressed()
    {
        // D=2, delta magnitude ≈ 0.5 → scale = 0.5/(0.5+2) = 0.2
        // Output magnitude ≈ 0.1 (heavily suppressed)
        var transform = new SoftDeadzoneTransform();
        var config = MakeConfig(deadzoneRadius: 2f);
        var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = config };

        transform.Apply(new InputSample(100f, 100f, 0f, 0f, false, false, 0), ctx);

        var input = new InputSample(100.5f, 100f, 0.5f, 0f, false, false, 1);
        ctx = new TransformContext { Tick = 1, Dt = 1f / 60f, Config = config };
        var result = transform.Apply(in input, ctx);

        // scale = 0.5 / (0.5 + 2) = 0.2 → outDx = 0.5*0.2 = 0.1
        float displacement = result.X - 100f;
        Assert.True(displacement < 0.5f * 0.5f,
            $"Small delta should be heavily suppressed: got displacement {displacement:F4}");
        Assert.True(displacement > 0f,
            "Displacement should be positive (not zero — quadratic, not hard cutoff)");
    }

    [Fact]
    public void LargeDelta_NearPassThrough()
    {
        // D=1, delta=20 → scale = 20/(20+1) = 0.952 → nearly pass-through
        var transform = new SoftDeadzoneTransform();
        var config = MakeConfig(deadzoneRadius: 1f);
        var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = config };

        transform.Apply(new InputSample(100f, 100f, 0f, 0f, false, false, 0), ctx);

        var input = new InputSample(120f, 100f, 20f, 0f, false, false, 1);
        ctx = new TransformContext { Tick = 1, Dt = 1f / 60f, Config = config };
        var result = transform.Apply(in input, ctx);

        float displacement = result.X - 100f;
        // scale = 20/21 ≈ 0.952, so displacement ≈ 19.05
        Assert.True(displacement > 18f,
            $"Large delta should be near pass-through: got {displacement:F2}, expected ~19.0");
        Assert.True(displacement < 20f,
            $"Large delta should be slightly compressed: got {displacement:F2}");
    }

    [Fact]
    public void QuadraticFormula_ExactValue()
    {
        // r=1, D=1 → scale = 1/(1+1) = 0.5 → Dx halved exactly
        var transform = new SoftDeadzoneTransform();
        var config = MakeConfig(deadzoneRadius: 1f);
        var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = config };

        transform.Apply(new InputSample(100f, 100f, 0f, 0f, false, false, 0), ctx);

        var input = new InputSample(101f, 100f, 1f, 0f, false, false, 1);
        ctx = new TransformContext { Tick = 1, Dt = 1f / 60f, Config = config };
        var result = transform.Apply(in input, ctx);

        Assert.Equal(0.5f, result.Dx, 4);
        Assert.Equal(100.5f, result.X, 4);
    }

    [Fact]
    public void Deterministic_SameInputsSameOutput()
    {
        var config = MakeConfig(deadzoneRadius: 1.5f);

        var t1 = new SoftDeadzoneTransform();
        var t2 = new SoftDeadzoneTransform();

        var samples = new[]
        {
            new InputSample(100f, 100f, 0f, 0f, false, false, 0),
            new InputSample(101f, 100.5f, 1f, 0.5f, false, false, 1),
            new InputSample(100.5f, 101f, -0.5f, 0.5f, false, false, 2),
            new InputSample(102f, 99f, 1.5f, -2f, false, false, 3),
        };

        for (int i = 0; i < samples.Length; i++)
        {
            var ctx = new TransformContext { Tick = i, Dt = 1f / 60f, Config = config };
            var r1 = t1.Apply(in samples[i], ctx);
            var r2 = t2.Apply(in samples[i], ctx);

            Assert.Equal(r1.X, r2.X);
            Assert.Equal(r1.Y, r2.Y);
            Assert.Equal(r1.Dx, r2.Dx);
            Assert.Equal(r1.Dy, r2.Dy);
        }
    }

    [Fact]
    public void AbsolutePositionConsistent()
    {
        // Over multiple ticks, accumulated output position should track correctly
        var transform = new SoftDeadzoneTransform();
        var config = MakeConfig(deadzoneRadius: 1f);

        var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = config };
        transform.Apply(new InputSample(0f, 0f, 0f, 0f, false, false, 0), ctx);

        // Feed 10 ticks of constant movement (dx=2 each)
        float expectedX = 0f;
        for (int i = 1; i <= 10; i++)
        {
            float rawX = i * 2f;
            ctx = new TransformContext { Tick = i, Dt = 1f / 60f, Config = config };
            var result = transform.Apply(
                new InputSample(rawX, 0f, 2f, 0f, false, false, i), ctx);

            // The output X should equal accumulated compressed deltas
            // scale = 2/(2+1) = 0.667, outDx = 2*0.667 = 1.333 per tick
            expectedX += 2f * (2f / (2f + 1f));
            Assert.Equal(expectedX, result.X, 3);
        }
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var transform = new SoftDeadzoneTransform();
        var config = MakeConfig(deadzoneRadius: 1f);

        var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = config };
        transform.Apply(new InputSample(100f, 100f, 0f, 0f, false, false, 0), ctx);
        transform.Apply(new InputSample(105f, 103f, 5f, 3f, false, false, 1), ctx);

        transform.Reset();

        // After reset, first sample should pass through (re-initialization)
        ctx = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = config };
        var result = transform.Apply(new InputSample(500f, 500f, 0f, 0f, false, false, 0), ctx);
        Assert.Equal(500f, result.X);
        Assert.Equal(500f, result.Y);
    }
}
