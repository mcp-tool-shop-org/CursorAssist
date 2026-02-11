using CursorAssist.Canon.Schemas;
using CursorAssist.Runtime.Core;
using Xunit;

namespace CursorAssist.Tests.Runtime;

/// <summary>
/// Tests for the output clamp layer: per-tick delta caps and runtime config
/// parameter enforcement. These are the last line of defense against pathological output.
/// </summary>
public class OutputClampTests
{
    // ── Delta clamping ──

    [Fact]
    public void ClampDelta_WithinLimits_Unchanged()
    {
        var (dx, dy) = EngineThread.ClampDelta(30f, -20f);
        Assert.Equal(30f, dx, 4);
        Assert.Equal(-20f, dy, 4);
    }

    [Fact]
    public void ClampDelta_ExceedsPositiveMax_Clamped()
    {
        var (dx, dy) = EngineThread.ClampDelta(100f, 200f);
        Assert.Equal(RuntimeLimits.MaxDeltaPerTick, dx, 4);
        Assert.Equal(RuntimeLimits.MaxDeltaPerTick, dy, 4);
    }

    [Fact]
    public void ClampDelta_ExceedsNegativeMax_Clamped()
    {
        var (dx, dy) = EngineThread.ClampDelta(-100f, -80f);
        Assert.Equal(-RuntimeLimits.MaxDeltaPerTick, dx, 4);
        Assert.Equal(-RuntimeLimits.MaxDeltaPerTick, dy, 4);
    }

    // ── Config clamping ──

    [Fact]
    public void ClampConfig_AlphaTooLow_ClampsToFloor()
    {
        var config = MakeConfig() with { SmoothingMinAlpha = 0.01f };
        var clamped = EngineThread.ClampConfig(config);
        Assert.Equal(RuntimeLimits.MinAlpha, clamped.SmoothingMinAlpha, 4);
    }

    [Fact]
    public void ClampConfig_AlphaTooHigh_ClampsToCeiling()
    {
        var config = MakeConfig() with { SmoothingMaxAlpha = 1.0f };
        var clamped = EngineThread.ClampConfig(config);
        Assert.Equal(RuntimeLimits.MaxAlpha, clamped.SmoothingMaxAlpha, 4);
    }

    [Fact]
    public void ClampConfig_DeadzoneOverMax_ClampsToMax()
    {
        var config = MakeConfig() with { DeadzoneRadiusVpx = 10f };
        var clamped = EngineThread.ClampConfig(config);
        Assert.Equal(RuntimeLimits.MaxDeadzoneRadius, clamped.DeadzoneRadiusVpx, 4);
    }

    [Fact]
    public void ClampConfig_PhaseCompOverMax_ClampsToMax()
    {
        var config = MakeConfig() with { PhaseCompensationGainS = 0.5f };
        var clamped = EngineThread.ClampConfig(config);
        Assert.Equal(RuntimeLimits.MaxPhaseCompGainS, clamped.PhaseCompensationGainS, 4);
    }

    [Fact]
    public void ClampConfig_ValidConfig_Unchanged()
    {
        var config = MakeConfig();
        var clamped = EngineThread.ClampConfig(config);

        Assert.Equal(config.SmoothingMinAlpha, clamped.SmoothingMinAlpha);
        Assert.Equal(config.SmoothingMaxAlpha, clamped.SmoothingMaxAlpha);
        Assert.Equal(config.DeadzoneRadiusVpx, clamped.DeadzoneRadiusVpx);
        Assert.Equal(config.PhaseCompensationGainS, clamped.PhaseCompensationGainS);
    }

    private static AssistiveConfig MakeConfig() => new()
    {
        SourceProfileId = "test",
        SmoothingMinAlpha = 0.25f,
        SmoothingMaxAlpha = 0.90f,
        DeadzoneRadiusVpx = 1.5f,
        PhaseCompensationGainS = 0.005f
    };
}
