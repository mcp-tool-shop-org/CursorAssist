using CursorAssist.Canon.Schemas;
using CursorAssist.Canon.Validation;
using Xunit;

namespace CursorAssist.Tests.Canon;

public class CanonValidationTests
{
    [Fact]
    public void ValidMotorProfile_PassesValidation()
    {
        var profile = MakeValidProfile();
        var result = CanonValidator.Validate(profile);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void MotorProfile_MissingProfileId_Fails()
    {
        var profile = MakeValidProfile() with { ProfileId = "" };
        var result = CanonValidator.Validate(profile);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ProfileId"));
    }

    [Fact]
    public void MotorProfile_PathEfficiencyOutOfRange_Fails()
    {
        var profile = MakeValidProfile() with { PathEfficiency = 1.5f };
        var result = CanonValidator.Validate(profile);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("PathEfficiency"));
    }

    [Fact]
    public void MotorProfile_NegativeTremor_Fails()
    {
        var profile = MakeValidProfile() with { TremorFrequencyHz = -1f };
        var result = CanonValidator.Validate(profile);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void MotorProfile_WrongVersion_Fails()
    {
        var profile = MakeValidProfile() with { Version = 99 };
        var result = CanonValidator.Validate(profile);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("version"));
    }

    [Fact]
    public void ValidAssistiveConfig_PassesValidation()
    {
        var config = MakeValidConfig();
        var result = CanonValidator.Validate(config);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void AssistiveConfig_StrengthOutOfRange_Fails()
    {
        var config = MakeValidConfig() with { MagnetismStrength = 1.5f };
        var result = CanonValidator.Validate(config);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidReport_PassesValidation()
    {
        var report = new UIAccessibilityReport
        {
            GeneratedUtc = DateTimeOffset.UtcNow,
            LayoutId = "test-layout",
            TrialCount = 10,
            ErrorRate = 0.1f
        };
        var result = CanonValidator.Validate(report);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void ValidAdaptivePlan_PassesValidation()
    {
        var plan = new AdaptiveDifficultyPlan
        {
            SourceProfileId = "prof-1",
            GeneratedUtc = DateTimeOffset.UtcNow,
            TargetSizeMultiplier = 1.0f,
            TargetSpeedMultiplier = 1.0f
        };
        var result = CanonValidator.Validate(plan);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void AdaptivePlan_SizeOutOfRange_Fails()
    {
        var plan = new AdaptiveDifficultyPlan
        {
            SourceProfileId = "prof-1",
            GeneratedUtc = DateTimeOffset.UtcNow,
            TargetSizeMultiplier = 10f
        };
        var result = CanonValidator.Validate(plan);
        Assert.False(result.IsValid);
    }

    private static MotorProfile MakeValidProfile() => new()
    {
        ProfileId = "test-profile",
        CreatedUtc = DateTimeOffset.UtcNow,
        PathEfficiency = 0.85f,
        TremorFrequencyHz = 5f,
        TremorAmplitudeVpx = 2f,
        OvershootRate = 0.3f,
        OvershootMagnitudeVpx = 15f,
        MeanTimeToTargetS = 0.8f,
        StdDevTimeToTargetS = 0.2f,
        ClickStabilityVpx = 3f,
        SampleCount = 50
    };

    // ── Phase compensation validation ──

    [Fact]
    public void PhaseCompGainS_Negative_Fails()
    {
        var config = MakeValidConfig() with { PhaseCompensationGainS = -0.01f };
        var result = CanonValidator.Validate(config);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("PhaseCompensationGainS"));
    }

    [Fact]
    public void PhaseCompGainS_TooHigh_Fails()
    {
        var config = MakeValidConfig() with { PhaseCompensationGainS = 0.2f };
        var result = CanonValidator.Validate(config);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("PhaseCompensationGainS"));
    }

    // ── Intent boost validation ──

    [Fact]
    public void IntentBoostStrength_OutOfRange_Fails()
    {
        var config = MakeValidConfig() with { IntentBoostStrength = 1.5f };
        var result = CanonValidator.Validate(config);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("IntentBoostStrength"));
    }

    [Fact]
    public void IntentCoherenceThreshold_TooLow_Fails()
    {
        var config = MakeValidConfig() with { IntentCoherenceThreshold = 0.3f };
        var result = CanonValidator.Validate(config);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("IntentCoherenceThreshold"));
    }

    private static AssistiveConfig MakeValidConfig() => new()
    {
        SourceProfileId = "test-profile",
        SmoothingStrength = 0.3f,
        SmoothingMinAlpha = 0.20f,
        SmoothingMaxAlpha = 0.90f,
        SmoothingVelocityLow = 0.5f,
        SmoothingVelocityHigh = 10f,
        MagnetismRadiusVpx = 80f,
        MagnetismStrength = 0.5f,
        MagnetismHysteresisVpx = 12f,
        EdgeResistance = 0.1f,
        SnapRadiusVpx = 3f,
        PhaseCompensationGainS = 0.005f,
        IntentBoostStrength = 0.4f,
        IntentCoherenceThreshold = 0.8f
    };
}
