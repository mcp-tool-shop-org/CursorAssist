using CursorAssist.Canon.Schemas;
using CursorAssist.Engine.Core;
using CursorAssist.Engine.Transforms;
using Xunit;

namespace CursorAssist.Tests.Transforms;

public class DirectionalIntentTransformTests
{
    private static AssistiveConfig MakeConfig(
        float strength = 0.5f, float threshold = 0.8f) => new()
    {
        SourceProfileId = "t",
        IntentBoostStrength = strength,
        IntentCoherenceThreshold = threshold
    };

    private static TransformContext Ctx(AssistiveConfig? config = null) =>
        new() { Tick = 1, Dt = 1f / 60f, Config = config };

    [Fact]
    public void ZeroStrength_PassesThrough()
    {
        var transform = new DirectionalIntentTransform();
        var config = MakeConfig(strength: 0f);
        var input = new InputSample(100f, 200f, 5f, 0f, false, false, 1);
        var result = transform.Apply(in input, Ctx(config));

        Assert.Equal(100f, result.X, 4);
        Assert.Equal(200f, result.Y, 4);
    }

    [Fact]
    public void NoConfig_PassesThrough()
    {
        var transform = new DirectionalIntentTransform();
        var input = new InputSample(100f, 200f, 5f, 0f, false, false, 1);
        var result = transform.Apply(in input, Ctx(config: null));

        Assert.Equal(100f, result.X, 4);
        Assert.Equal(200f, result.Y, 4);
    }

    [Fact]
    public void FirstTick_PassesThrough()
    {
        // First tick always passes through (no previous delta to compare)
        var transform = new DirectionalIntentTransform();
        var config = MakeConfig(strength: 1f);
        var input = new InputSample(100f, 200f, 5f, 0f, false, false, 0);
        var result = transform.Apply(in input, Ctx(config));

        Assert.Equal(100f, result.X, 4);
        Assert.Equal(200f, result.Y, 4);
    }

    [Fact]
    public void ConsistentDirection_BoostsAfterWarmup()
    {
        // Feed 30+ ticks of constant rightward motion → coherence EMA should exceed threshold
        var transform = new DirectionalIntentTransform();
        var config = MakeConfig(strength: 0.8f, threshold: 0.8f);

        InputSample result = default;
        float baselineX = 0f;

        for (int i = 0; i < 40; i++)
        {
            float x = i * 5f;
            var input = new InputSample(x, 0f, 5f, 0f, false, false, i);
            result = transform.Apply(in input, Ctx(config));
            baselineX = x;
        }

        // After 40 consistent ticks, the boost should have kicked in:
        // X should be > baseline (boosted forward)
        Assert.True(result.X > baselineX,
            $"After consistent motion, X ({result.X:F4}) should be > baseline ({baselineX:F4})");
    }

    [Fact]
    public void AlternatingDirection_NoBoost()
    {
        // Alternating directions → low coherence → no boost
        var transform = new DirectionalIntentTransform();
        var config = MakeConfig(strength: 1f, threshold: 0.8f);

        InputSample result = default;
        for (int i = 0; i < 40; i++)
        {
            float dx = (i % 2 == 0) ? 5f : -5f;
            float x = i * 2f; // Arbitrary position
            var input = new InputSample(x, 0f, dx, 0f, false, false, i);
            result = transform.Apply(in input, Ctx(config));
        }

        // Last tick has dx=-5 → position should not be boosted beyond input X
        // With alternating: coherence should be near -1, EMA stays low → no boost
        float lastX = 39 * 2f;
        Assert.Equal(lastX, result.X, 1); // No significant boost
    }

    [Fact]
    public void BelowVelocityFloor_NoCoherence()
    {
        // Very small deltas (below VelocityFloor=0.1) → coherence stays at 0
        var transform = new DirectionalIntentTransform();
        var config = MakeConfig(strength: 1f, threshold: 0.8f);

        InputSample result = default;
        for (int i = 0; i < 40; i++)
        {
            var input = new InputSample(i * 0.01f, 0f, 0.01f, 0f, false, false, i);
            result = transform.Apply(in input, Ctx(config));
        }

        // Very small velocity → no coherence computed → no boost
        Assert.Equal(39 * 0.01f, result.X, 3);
    }

    [Fact]
    public void Deterministic_SameInputsSameOutput()
    {
        var config = MakeConfig(strength: 0.5f);
        var t1 = new DirectionalIntentTransform();
        var t2 = new DirectionalIntentTransform();

        for (int i = 0; i < 20; i++)
        {
            var input = new InputSample(i * 3f, 0f, 3f, 0f, false, false, i);
            var r1 = t1.Apply(in input, Ctx(config));
            var r2 = t2.Apply(in input, Ctx(config));

            Assert.Equal(r1.X, r2.X);
            Assert.Equal(r1.Y, r2.Y);
        }
    }

    [Fact]
    public void Reset_ClearsCoherence()
    {
        var transform = new DirectionalIntentTransform();
        var config = MakeConfig(strength: 0.8f, threshold: 0.8f);

        // Build up coherence
        for (int i = 0; i < 30; i++)
        {
            var input = new InputSample(i * 5f, 0f, 5f, 0f, false, false, i);
            transform.Apply(in input, Ctx(config));
        }

        transform.Reset();

        // After reset, first tick should pass through (no boost)
        var postReset = new InputSample(0f, 0f, 5f, 0f, false, false, 0);
        var result = transform.Apply(in postReset, Ctx(config));
        Assert.Equal(0f, result.X, 4);
    }

    [Fact]
    public void OnlyModifiesXY_NotDxDy()
    {
        var transform = new DirectionalIntentTransform();
        var config = MakeConfig(strength: 1f, threshold: 0.5f);

        // Warm up with consistent direction
        for (int i = 0; i < 30; i++)
        {
            var s = new InputSample(i * 5f, 0f, 5f, 0f, false, false, i);
            transform.Apply(in s, Ctx(config));
        }

        // Next tick — even if boost is applied, Dx/Dy should be unchanged
        var input = new InputSample(150f, 0f, 5f, 2f, false, false, 30);
        var result = transform.Apply(in input, Ctx(config));

        Assert.Equal(5f, result.Dx, 4);
        Assert.Equal(2f, result.Dy, 4);
    }

    [Fact]
    public void BoostProportionalToCoherence()
    {
        // Compare boost at two different thresholds — lower threshold → more boost
        // (because ramp = (ema - threshold) / (1 - threshold) is larger)
        var t1 = new DirectionalIntentTransform();
        var t2 = new DirectionalIntentTransform();
        var configLow = MakeConfig(strength: 0.5f, threshold: 0.6f);
        var configHigh = MakeConfig(strength: 0.5f, threshold: 0.9f);

        InputSample r1 = default, r2 = default;
        for (int i = 0; i < 40; i++)
        {
            var input = new InputSample(i * 5f, 0f, 5f, 0f, false, false, i);
            r1 = t1.Apply(in input, Ctx(configLow));
            r2 = t2.Apply(in input, Ctx(configHigh));
        }

        // Lower threshold should result in more boost (X further ahead)
        Assert.True(r1.X >= r2.X,
            $"Lower threshold should give >= boost: low={r1.X:F4}, high={r2.X:F4}");
    }
}
