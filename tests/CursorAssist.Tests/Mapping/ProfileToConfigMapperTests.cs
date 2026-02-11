using CursorAssist.Canon.Schemas;
using CursorAssist.Engine.Mapping;
using Xunit;

namespace CursorAssist.Tests.Mapping;

public class ProfileToConfigMapperTests
{
    [Fact]
    public void Deterministic_SameProfile_SameConfig()
    {
        var profile = MakeProfile(tremor: 5f, pathEff: 0.7f, overshoot: 0.5f);
        var c1 = ProfileToConfigMapper.Map(profile);
        var c2 = ProfileToConfigMapper.Map(profile);

        Assert.Equal(c1.SmoothingStrength, c2.SmoothingStrength);
        Assert.Equal(c1.MagnetismRadiusVpx, c2.MagnetismRadiusVpx);
        Assert.Equal(c1.MagnetismStrength, c2.MagnetismStrength);
        Assert.Equal(c1.EdgeResistance, c2.EdgeResistance);
        Assert.Equal(c1.SnapRadiusVpx, c2.SnapRadiusVpx);
    }

    [Fact]
    public void HighTremor_IncreasesSmoothing()
    {
        var low = ProfileToConfigMapper.Map(MakeProfile(tremor: 1f));
        var high = ProfileToConfigMapper.Map(MakeProfile(tremor: 8f));

        Assert.True(high.SmoothingStrength > low.SmoothingStrength);
    }

    [Fact]
    public void PoorPathEfficiency_IncreasesRadius()
    {
        var good = ProfileToConfigMapper.Map(MakeProfile(pathEff: 0.95f));
        var poor = ProfileToConfigMapper.Map(MakeProfile(pathEff: 0.5f));

        Assert.True(poor.MagnetismRadiusVpx > good.MagnetismRadiusVpx);
    }

    [Fact]
    public void HighOvershoot_IncreasesEdgeResistance()
    {
        var low = ProfileToConfigMapper.Map(MakeProfile(overshoot: 0.1f));
        var high = ProfileToConfigMapper.Map(MakeProfile(overshoot: 1.5f));

        Assert.True(high.EdgeResistance > low.EdgeResistance);
    }

    [Fact]
    public void SignificantTremor_EnablesSnap()
    {
        var noTremor = ProfileToConfigMapper.Map(MakeProfile(tremor: 1f));
        var tremor = ProfileToConfigMapper.Map(MakeProfile(tremor: 5f));

        Assert.Equal(0f, noTremor.SnapRadiusVpx);
        Assert.True(tremor.SnapRadiusVpx > 0f);
    }

    [Fact]
    public void OutputIsValid()
    {
        var config = ProfileToConfigMapper.Map(MakeProfile());
        Assert.True(config.SmoothingStrength >= 0f && config.SmoothingStrength <= 1f);
        Assert.True(config.MagnetismStrength >= 0f && config.MagnetismStrength <= 1f);
        Assert.True(config.EdgeResistance >= 0f && config.EdgeResistance <= 1f);
        Assert.True(config.MagnetismRadiusVpx >= 0f);
        Assert.True(config.MagnetismHysteresisVpx >= 0f);
    }

    [Fact]
    public void PolicyVersion_IsSet()
    {
        var config = ProfileToConfigMapper.Map(MakeProfile());
        Assert.Equal(ProfileToConfigMapper.PolicyVersion, config.MappingPolicyVersion);
    }

    [Fact]
    public void SourceProfileId_IsPreserved()
    {
        var profile = MakeProfile();
        var config = ProfileToConfigMapper.Map(profile);
        Assert.Equal(profile.ProfileId, config.SourceProfileId);
    }

    private static MotorProfile MakeProfile(
        float tremor = 3f,
        float pathEff = 0.8f,
        float overshoot = 0.4f) => new()
    {
        ProfileId = "test-profile",
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
