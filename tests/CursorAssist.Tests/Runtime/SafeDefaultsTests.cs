using CursorAssist.Canon.Validation;
using CursorAssist.Runtime.Core;
using Xunit;

namespace CursorAssist.Tests.Runtime;

public class SafeDefaultsTests
{
    [Fact]
    public void Minimal_PassesCanonValidation()
    {
        var config = SafeDefaults.Minimal();

        var result = CanonValidator.Validate(config);

        Assert.True(result.IsValid,
            $"Minimal config failed validation: {string.Join("; ", result.Errors)}");

        // Verify key parameters are conservative
        Assert.Equal("safe-default-minimal", config.SourceProfileId);
        Assert.InRange(config.SmoothingStrength, 0f, 0.5f);
        Assert.InRange(config.DeadzoneRadiusVpx, 0f, 1.0f);
        Assert.Equal(0f, config.PhaseCompensationGainS); // Disabled
        Assert.Equal(0f, config.IntentBoostStrength);     // Disabled
        Assert.Equal(0f, config.MagnetismStrength);       // Disabled
    }

    [Fact]
    public void Moderate_PassesCanonValidation()
    {
        var config = SafeDefaults.Moderate();

        var result = CanonValidator.Validate(config);

        Assert.True(result.IsValid,
            $"Moderate config failed validation: {string.Join("; ", result.Errors)}");

        // Verify key parameters are sensible
        Assert.Equal("safe-default-moderate", config.SourceProfileId);
        Assert.InRange(config.SmoothingStrength, 0.3f, 0.7f);
        Assert.InRange(config.DeadzoneRadiusVpx, 0.5f, 2.0f);
        Assert.True(config.PhaseCompensationGainS > 0f, "Phase comp should be enabled");
        Assert.True(config.IntentBoostStrength > 0f, "Intent boost should be enabled");

        // Phase comp within safe range
        Assert.InRange(config.PhaseCompensationGainS, 0f, RuntimeLimits.MaxPhaseCompGainS);
    }

    [Fact]
    public void Minimal_RunsStablyThroughFullPipeline()
    {
        // Verify the minimal config doesn't cause any NaN/Infinity when run through engine
        var config = SafeDefaults.Minimal();
        var pipeline = Tests.Helpers.TestStreamGenerator.BuildFullPipeline();
        var engine = new CursorAssist.Engine.Core.DeterministicPipeline(pipeline);
        var events = Tests.Helpers.TestStreamGenerator.GenerateDeterministicStream(600, seed: 99);

        float curX = 0f, curY = 0f;
        for (int i = 0; i < events.Count; i++)
        {
            var evt = events[i];
            curX += evt.Dx;
            curY += evt.Dy;

            var input = new CursorAssist.Engine.Core.InputSample(
                curX, curY, evt.Dx, evt.Dy, evt.PrimaryDown, evt.SecondaryDown, i);

            var ctx = new CursorAssist.Engine.Core.TransformContext
            {
                Tick = i,
                Dt = 1f / 60f,
                Config = config
            };

            var result = engine.FixedStep(in input, ctx);
            Assert.False(float.IsNaN(result.FinalCursor.X), $"NaN at tick {i}");
            Assert.False(float.IsInfinity(result.FinalCursor.X), $"Infinity at tick {i}");
        }
    }

    [Fact]
    public void Moderate_RunsStablyThroughFullPipeline()
    {
        var config = SafeDefaults.Moderate();
        var pipeline = Tests.Helpers.TestStreamGenerator.BuildFullPipeline();
        var engine = new CursorAssist.Engine.Core.DeterministicPipeline(pipeline);
        var events = Tests.Helpers.TestStreamGenerator.GenerateDeterministicStream(600, seed: 99);

        float curX = 0f, curY = 0f;
        for (int i = 0; i < events.Count; i++)
        {
            var evt = events[i];
            curX += evt.Dx;
            curY += evt.Dy;

            var input = new CursorAssist.Engine.Core.InputSample(
                curX, curY, evt.Dx, evt.Dy, evt.PrimaryDown, evt.SecondaryDown, i);

            var ctx = new CursorAssist.Engine.Core.TransformContext
            {
                Tick = i,
                Dt = 1f / 60f,
                Config = config
            };

            var result = engine.FixedStep(in input, ctx);
            Assert.False(float.IsNaN(result.FinalCursor.X), $"NaN at tick {i}");
            Assert.False(float.IsInfinity(result.FinalCursor.X), $"Infinity at tick {i}");
        }
    }
}
