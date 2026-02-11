using CursorAssist.Canon.Schemas;
using CursorAssist.Policy;
using Xunit;

namespace CursorAssist.Tests.Policy;

public class PolicyMapperTests
{
    [Fact]
    public void Deterministic_SameProfileSameConfig()
    {
        var profile = MakeProfile(tremor: 5f, pathEff: 0.7f, overshoot: 0.5f);
        var c1 = ProfileToConfigMapper.Map(profile);
        var c2 = ProfileToConfigMapper.Map(profile);

        Assert.Equal(c1.SmoothingStrength, c2.SmoothingStrength);
        Assert.Equal(c1.SmoothingMinAlpha, c2.SmoothingMinAlpha);
        Assert.Equal(c1.SmoothingMaxAlpha, c2.SmoothingMaxAlpha);
        Assert.Equal(c1.SmoothingVelocityLow, c2.SmoothingVelocityLow);
        Assert.Equal(c1.SmoothingVelocityHigh, c2.SmoothingVelocityHigh);
        Assert.Equal(c1.MagnetismRadiusVpx, c2.MagnetismRadiusVpx);
        Assert.Equal(c1.MagnetismStrength, c2.MagnetismStrength);
        Assert.Equal(c1.DeadzoneRadiusVpx, c2.DeadzoneRadiusVpx);
        Assert.Equal(c1.SmoothingDualPoleEnabled, c2.SmoothingDualPoleEnabled);
    }

    [Fact]
    public void IdenticalToEngineMapper()
    {
        // Policy mapper must produce identical output to the Engine version
        var profile = MakeProfile();
        var policyConfig = ProfileToConfigMapper.Map(profile);
        var engineConfig = CursorAssist.Engine.Mapping.ProfileToConfigMapper.Map(profile);

        Assert.Equal(policyConfig.SmoothingStrength, engineConfig.SmoothingStrength);
        Assert.Equal(policyConfig.SmoothingMinAlpha, engineConfig.SmoothingMinAlpha);
        Assert.Equal(policyConfig.SmoothingMaxAlpha, engineConfig.SmoothingMaxAlpha);
        Assert.Equal(policyConfig.SmoothingVelocityLow, engineConfig.SmoothingVelocityLow);
        Assert.Equal(policyConfig.SmoothingVelocityHigh, engineConfig.SmoothingVelocityHigh);
        Assert.Equal(policyConfig.MagnetismRadiusVpx, engineConfig.MagnetismRadiusVpx);
        Assert.Equal(policyConfig.MagnetismStrength, engineConfig.MagnetismStrength);
        Assert.Equal(policyConfig.EdgeResistance, engineConfig.EdgeResistance);
        Assert.Equal(policyConfig.SnapRadiusVpx, engineConfig.SnapRadiusVpx);
        Assert.Equal(policyConfig.SmoothingAdaptiveFrequencyEnabled, engineConfig.SmoothingAdaptiveFrequencyEnabled);
        Assert.Equal(policyConfig.DeadzoneRadiusVpx, engineConfig.DeadzoneRadiusVpx);
        Assert.Equal(policyConfig.SmoothingDualPoleEnabled, engineConfig.SmoothingDualPoleEnabled);
    }

    [Fact]
    public void PolicyVersionIsSet()
    {
        var config = ProfileToConfigMapper.Map(MakeProfile());
        Assert.Equal(2, config.MappingPolicyVersion);
        Assert.Equal(ProfileToConfigMapper.PolicyVersion, config.MappingPolicyVersion);
    }

    private static MotorProfile MakeProfile(
        float tremor = 3f, float pathEff = 0.8f, float overshoot = 0.4f) => new()
    {
        ProfileId = "test",
        CreatedUtc = DateTimeOffset.UtcNow,
        TremorAmplitudeVpx = tremor,
        TremorFrequencyHz = tremor > 0 ? 6f : 0f,
        PathEfficiency = pathEff,
        OvershootRate = overshoot,
        OvershootMagnitudeVpx = overshoot * 20f,
        MeanTimeToTargetS = 0.8f,
        StdDevTimeToTargetS = 0.2f,
        ClickStabilityVpx = 2f,
        SampleCount = 100
    };
}
