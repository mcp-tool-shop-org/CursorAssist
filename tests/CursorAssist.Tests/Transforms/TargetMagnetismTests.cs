using CursorAssist.Canon.Schemas;
using CursorAssist.Engine.Core;
using CursorAssist.Engine.Transforms;
using Xunit;

namespace CursorAssist.Tests.Transforms;

public class TargetMagnetismTests
{
    private static readonly AssistiveConfig DefaultConfig = new()
    {
        SourceProfileId = "test",
        MagnetismRadiusVpx = 100f,
        MagnetismStrength = 0.8f,
        MagnetismHysteresisVpx = 20f,
        SnapRadiusVpx = 5f
    };

    private static readonly TargetInfo Target = new("btn-1", 500f, 300f, 60f, 30f);

    [Fact]
    public void OutsideRadius_NoEffect()
    {
        var transform = new TargetMagnetismTransform();
        var input = new InputSample(900f, 900f, 0f, 0f, false, false, 0);
        var ctx = MakeContext(input.X, input.Y);

        var result = transform.Apply(in input, ctx);

        Assert.Equal(input.X, result.X);
        Assert.Equal(input.Y, result.Y);
    }

    [Fact]
    public void InsideRadius_PullsTowardCenter()
    {
        var transform = new TargetMagnetismTransform();
        // 50px from center (within 100px radius)
        var input = new InputSample(550f, 300f, 0f, 0f, false, false, 0);
        var ctx = MakeContext(input.X, input.Y);

        var result = transform.Apply(in input, ctx);

        // Should be pulled toward 500, 300
        Assert.True(result.X < input.X, "X should be pulled left toward center");
        Assert.Equal(input.Y, result.Y, 0.01f); // Y unchanged (already at center Y)
    }

    [Fact]
    public void InsideSnapRadius_SnapsToCenter()
    {
        var transform = new TargetMagnetismTransform();
        // 3px from center (within 5px snap radius)
        var input = new InputSample(503f, 300f, 0f, 0f, false, false, 0);
        var ctx = MakeContext(input.X, input.Y);

        var result = transform.Apply(in input, ctx);

        Assert.Equal(Target.CenterX, result.X);
        Assert.Equal(Target.CenterY, result.Y);
    }

    [Fact]
    public void Hysteresis_DoesNotFlicker()
    {
        var transform = new TargetMagnetismTransform();

        // Enter the radius at 80px from center
        var enter = new InputSample(580f, 300f, 0f, 0f, false, false, 0);
        var ctx = MakeContext(enter.X, enter.Y);
        var r1 = transform.Apply(in enter, ctx);
        Assert.True(r1.X < enter.X, "Should engage");

        // Move to 110px (beyond radius, within hysteresis buffer)
        var hover = new InputSample(610f, 300f, 0f, 0f, false, false, 1);
        ctx = MakeContext(hover.X, hover.Y);
        var r2 = transform.Apply(in hover, ctx);
        Assert.True(r2.X < hover.X, "Should still be engaged (hysteresis)");

        // Move to 130px (beyond radius + hysteresis = 120px)
        var leave = new InputSample(630f, 300f, 0f, 0f, false, false, 2);
        ctx = MakeContext(leave.X, leave.Y);
        var r3 = transform.Apply(in leave, ctx);
        Assert.Equal(leave.X, r3.X, 0.01f); // Disengaged
    }

    [Fact]
    public void NoConfig_PassesThrough()
    {
        var transform = new TargetMagnetismTransform();
        var input = new InputSample(510f, 300f, 0f, 0f, false, false, 0);
        var ctx = new TransformContext
        {
            Tick = 0,
            Dt = 1f / 60f,
            Targets = [Target],
            Config = null
        };

        var result = transform.Apply(in input, ctx);
        Assert.Equal(input.X, result.X);
    }

    [Fact]
    public void NoTargets_PassesThrough()
    {
        var transform = new TargetMagnetismTransform();
        var input = new InputSample(510f, 300f, 0f, 0f, false, false, 0);
        var ctx = new TransformContext
        {
            Tick = 0,
            Dt = 1f / 60f,
            Targets = [],
            Config = DefaultConfig
        };

        var result = transform.Apply(in input, ctx);
        Assert.Equal(input.X, result.X);
    }

    [Fact]
    public void CloserToCenter_StrongerPull()
    {
        var transform1 = new TargetMagnetismTransform();
        var transform2 = new TargetMagnetismTransform();

        // 30px from center
        var close = new InputSample(530f, 300f, 0f, 0f, false, false, 0);
        var ctxClose = MakeContext(close.X, close.Y);
        var r1 = transform1.Apply(in close, ctxClose);

        // 80px from center
        var far = new InputSample(580f, 300f, 0f, 0f, false, false, 0);
        var ctxFar = MakeContext(far.X, far.Y);
        var r2 = transform2.Apply(in far, ctxFar);

        // Close input should be pulled proportionally more
        float pullClose = close.X - r1.X;
        float pullFar = far.X - r2.X;
        float pullRatioClose = pullClose / (close.X - Target.CenterX);
        float pullRatioFar = pullFar / (far.X - Target.CenterX);

        Assert.True(pullRatioClose > pullRatioFar, "Closer cursor should have stronger pull ratio");
    }

    [Fact]
    public void Reset_ClearsEngagedState()
    {
        var transform = new TargetMagnetismTransform();

        // Engage
        var input = new InputSample(550f, 300f, 0f, 0f, false, false, 0);
        transform.Apply(in input, MakeContext(input.X, input.Y));

        // Reset
        transform.Reset();

        // Far away — should not be engaged
        var far = new InputSample(900f, 900f, 0f, 0f, false, false, 1);
        var result = transform.Apply(in far, MakeContext(far.X, far.Y));
        Assert.Equal(far.X, result.X);
    }

    [Fact]
    public void Deterministic_SameInputsSameOutput()
    {
        var t1 = new TargetMagnetismTransform();
        var t2 = new TargetMagnetismTransform();

        var input = new InputSample(540f, 310f, 0f, 0f, false, false, 0);
        var ctx = MakeContext(input.X, input.Y);

        var r1 = t1.Apply(in input, ctx);
        var r2 = t2.Apply(in input, ctx);

        Assert.Equal(r1.X, r2.X);
        Assert.Equal(r1.Y, r2.Y);
    }

    private static TransformContext MakeContext(float cursorX, float cursorY) => new()
    {
        Tick = 0,
        Dt = 1f / 60f,
        Targets = [Target],
        Config = DefaultConfig
    };
}
