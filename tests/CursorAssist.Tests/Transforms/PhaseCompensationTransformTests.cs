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
        // gain=0.01s, Dx=2, Fs=60 → X += 0.01 × 2 × 60 = 1.2
        var transform = new PhaseCompensationTransform();
        var input = new InputSample(50f, 80f, 2f, 0f, false, false, 1);
        var result = transform.Apply(in input, Ctx(MakeConfig(gainS: 0.01f)));

        Assert.Equal(50f + 1.2f, result.X, 4);
        Assert.Equal(80f, result.Y, 4);
    }

    [Fact]
    public void NegativeVelocity_ProjectsBackward()
    {
        // gain=0.01s, Dx=-3 → X += 0.01 × (-3) × 60 = -1.8
        var transform = new PhaseCompensationTransform();
        var input = new InputSample(50f, 80f, -3f, 0f, false, false, 1);
        var result = transform.Apply(in input, Ctx(MakeConfig(gainS: 0.01f)));

        Assert.Equal(50f - 1.8f, result.X, 4);
        Assert.Equal(80f, result.Y, 4);
    }

    [Fact]
    public void DiagonalMovement_BothAxes()
    {
        // gain=0.02s, Dx=1, Dy=3 → X += 0.02*1*60=1.2, Y += 0.02*3*60=3.6
        var transform = new PhaseCompensationTransform();
        var input = new InputSample(10f, 20f, 1f, 3f, false, false, 1);
        var result = transform.Apply(in input, Ctx(MakeConfig(gainS: 0.02f)));

        Assert.Equal(10f + 1.2f, result.X, 4);
        Assert.Equal(20f + 3.6f, result.Y, 4);
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
}
