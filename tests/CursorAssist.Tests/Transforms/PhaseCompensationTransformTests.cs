using CursorAssist.Canon.Schemas;
using CursorAssist.Engine.Core;
using CursorAssist.Engine.Transforms;
using Xunit;

namespace CursorAssist.Tests.Transforms;

public class PhaseCompensationTransformTests
{
    private static AssistiveConfig MakeConfig(float gainS = 0.01f) => new()
    {
        SourceProfileId = "t",
        PhaseCompensationGainS = gainS
    };

    private static TransformContext Ctx(AssistiveConfig? config = null) =>
        new() { Tick = 1, Dt = 1f / 60f, Config = config };

    [Fact]
    public void ZeroGain_PassesThrough()
    {
        var transform = new PhaseCompensationTransform();
        var input = new InputSample(100f, 200f, 3f, -2f, false, false, 1);
        var result = transform.Apply(in input, Ctx(MakeConfig(gainS: 0f)));

        Assert.Equal(100f, result.X, 4);
        Assert.Equal(200f, result.Y, 4);
    }

    [Fact]
    public void NoConfig_PassesThrough()
    {
        var transform = new PhaseCompensationTransform();
        var input = new InputSample(100f, 200f, 3f, -2f, false, false, 1);
        var result = transform.Apply(in input, Ctx(config: null));

        Assert.Equal(100f, result.X, 4);
        Assert.Equal(200f, result.Y, 4);
    }

    [Fact]
    public void PositiveGain_ProjectsForward()
    {
        // gain=0.01s, Dx=2, Dy=0, velocity=2
        // v4: effectiveGain = 0.01 / (1 + 2/15) = 0.01/1.1333 ≈ 0.008824
        // X += 0.008824 × 2 × 60 ≈ 1.0588
        var transform = new PhaseCompensationTransform();
        var input = new InputSample(50f, 80f, 2f, 0f, false, false, 1);
        var result = transform.Apply(in input, Ctx(MakeConfig(gainS: 0.01f)));

        float effectiveGain = 0.01f / (1f + 2f / 15f);
        Assert.Equal(50f + effectiveGain * 2f * 60f, result.X, 3);
        Assert.Equal(80f, result.Y, 4);
    }

    [Fact]
    public void NegativeVelocity_ProjectsBackward()
    {
        // gain=0.01s, Dx=-3, Dy=0, velocity=3
        // v4: effectiveGain = 0.01 / (1 + 3/15) = 0.01/1.2 = 0.008333
        // X += 0.008333 × (-3) × 60 = -1.5
        var transform = new PhaseCompensationTransform();
        var input = new InputSample(50f, 80f, -3f, 0f, false, false, 1);
        var result = transform.Apply(in input, Ctx(MakeConfig(gainS: 0.01f)));

        float effectiveGain = 0.01f / (1f + 3f / 15f);
        Assert.Equal(50f + effectiveGain * -3f * 60f, result.X, 3);
        Assert.Equal(80f, result.Y, 4);
    }

    [Fact]
    public void DiagonalMovement_BothAxes()
    {
        // gain=0.02s, Dx=1, Dy=3, velocity=√10≈3.162
        // v4: effectiveGain = 0.02 / (1 + √10/15)
        var transform = new PhaseCompensationTransform();
        var input = new InputSample(10f, 20f, 1f, 3f, false, false, 1);
        var result = transform.Apply(in input, Ctx(MakeConfig(gainS: 0.02f)));

        float velocity = MathF.Sqrt(1f * 1f + 3f * 3f);
        float effectiveGain = 0.02f / (1f + velocity / 15f);
        Assert.Equal(10f + effectiveGain * 1f * 60f, result.X, 3);
        Assert.Equal(20f + effectiveGain * 3f * 60f, result.Y, 3);
    }

    [Fact]
    public void ZeroVelocity_NoOffset()
    {
        var transform = new PhaseCompensationTransform();
        var input = new InputSample(100f, 200f, 0f, 0f, false, false, 1);
        var result = transform.Apply(in input, Ctx(MakeConfig(gainS: 0.05f)));

        Assert.Equal(100f, result.X, 4);
        Assert.Equal(200f, result.Y, 4);
    }

    [Fact]
    public void Deterministic_SameInputsSameOutput()
    {
        var config = MakeConfig(gainS: 0.015f);
        var t1 = new PhaseCompensationTransform();
        var t2 = new PhaseCompensationTransform();

        var input = new InputSample(50f, 60f, 4f, -1f, false, false, 1);
        var r1 = t1.Apply(in input, Ctx(config));
        var r2 = t2.Apply(in input, Ctx(config));

        Assert.Equal(r1.X, r2.X);
        Assert.Equal(r1.Y, r2.Y);
    }

    [Fact]
    public void Reset_IsNoOp_StillWorks()
    {
        var transform = new PhaseCompensationTransform();
        var config = MakeConfig(gainS: 0.01f);
        var input = new InputSample(50f, 80f, 2f, 0f, false, false, 1);

        var before = transform.Apply(in input, Ctx(config));
        transform.Reset();
        var after = transform.Apply(in input, Ctx(config));

        Assert.Equal(before.X, after.X);
        Assert.Equal(before.Y, after.Y);
    }

    [Fact]
    public void DoesNotModifyDxDy()
    {
        var transform = new PhaseCompensationTransform();
        var input = new InputSample(10f, 20f, 5f, -3f, false, false, 1);
        var result = transform.Apply(in input, Ctx(MakeConfig(gainS: 0.05f)));

        Assert.Equal(5f, result.Dx, 4);
        Assert.Equal(-3f, result.Dy, 4);
    }

    // ── v4 velocity-dependent attenuation tests ──

    [Fact]
    public void HighVelocity_AttenuatesCompensation()
    {
        // At velocity=30 vpx/tick, effectiveGain = gainS / (1 + 30/15) = gainS / 3
        // Compensation offset should be < 50% of what linear (no saturation) would produce
        var transform = new PhaseCompensationTransform();
        var input = new InputSample(0f, 0f, 30f, 0f, false, false, 1);
        var result = transform.Apply(in input, Ctx(MakeConfig(gainS: 0.01f)));

        float linearOffset = 0.01f * 30f * 60f; // 18.0 without attenuation
        float actualOffset = result.X - 0f;
        Assert.True(actualOffset < linearOffset * 0.5f,
            $"At v=30, offset ({actualOffset:F3}) should be < 50% of linear ({linearOffset:F3})");
        Assert.True(actualOffset > 0f, "Offset should still be positive (forward)");
    }

    [Fact]
    public void LowVelocity_NearFullCompensation()
    {
        // At velocity=1 vpx/tick, effectiveGain = gainS / (1 + 1/15) ≈ gainS × 0.9375
        // Compensation offset should be > 90% of linear
        var transform = new PhaseCompensationTransform();
        var input = new InputSample(0f, 0f, 1f, 0f, false, false, 1);
        var result = transform.Apply(in input, Ctx(MakeConfig(gainS: 0.01f)));

        float linearOffset = 0.01f * 1f * 60f; // 0.6 without attenuation
        float actualOffset = result.X - 0f;
        Assert.True(actualOffset > linearOffset * 0.90f,
            $"At v=1, offset ({actualOffset:F4}) should be > 90% of linear ({linearOffset:F4})");
    }

    [Fact]
    public void VelocitySaturation_HalvesGainAtKnee()
    {
        // At VelocitySaturation=15 vpx/tick, gain should be exactly halved
        // effectiveGain = gainS / (1 + 15/15) = gainS / 2
        var transform = new PhaseCompensationTransform();
        var input = new InputSample(0f, 0f, 15f, 0f, false, false, 1);
        var result = transform.Apply(in input, Ctx(MakeConfig(gainS: 0.02f)));

        float expectedOffset = (0.02f / 2f) * 15f * 60f; // 9.0
        Assert.Equal(expectedOffset, result.X, 3);
    }
}
