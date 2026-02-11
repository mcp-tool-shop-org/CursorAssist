using CursorAssist.Canon.Schemas;
using CursorAssist.Engine.Core;
using CursorAssist.Engine.Transforms;
using Xunit;

namespace CursorAssist.Tests.Transforms;

public class SmoothingTransformTests
{
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
        var config = new AssistiveConfig { SourceProfileId = "t", SmoothingStrength = 0f };
        var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = config };

        var input = new InputSample(100f, 200f, 0f, 0f, false, false, 0);
        var result = transform.Apply(in input, ctx);
        Assert.Equal(100f, result.X);
    }

    [Fact]
    public void HighStrength_SmoothsJitter()
    {
        var transform = new SmoothingTransform();
        var config = new AssistiveConfig { SourceProfileId = "t", SmoothingStrength = 0.8f };

        // Initialize
        var init = new InputSample(100f, 100f, 0f, 0f, false, false, 0);
        var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = config };
        transform.Apply(in init, ctx);

        // Jitter: sudden jump
        var jitter = new InputSample(120f, 100f, 20f, 0f, false, false, 1);
        ctx = new TransformContext { Tick = 1, Dt = 1f / 60f, Config = config };
        var result = transform.Apply(in jitter, ctx);

        // Smoothed output should be between old and new
        Assert.True(result.X > 100f, "Should move toward jitter");
        Assert.True(result.X < 120f, "Should not fully follow jitter");
    }

    [Fact]
    public void Deterministic_SameInputsSameOutput()
    {
        var t1 = new SmoothingTransform();
        var t2 = new SmoothingTransform();
        var config = new AssistiveConfig { SourceProfileId = "t", SmoothingStrength = 0.5f };
        var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f, Config = config };

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
            ctx = new TransformContext { Tick = i, Dt = 1f / 60f, Config = config };
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
        var config = new AssistiveConfig { SourceProfileId = "t", SmoothingStrength = 0.9f };

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
}
