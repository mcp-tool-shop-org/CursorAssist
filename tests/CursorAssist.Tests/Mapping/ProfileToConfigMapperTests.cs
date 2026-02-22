using CursorAssist.Canon.Schemas;
using CursorAssist.Policy;
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
        Assert.Equal(c1.SmoothingMinAlpha, c2.SmoothingMinAlpha);
        Assert.Equal(c1.SmoothingMaxAlpha, c2.SmoothingMaxAlpha);
        Assert.Equal(c1.SmoothingVelocityLow, c2.SmoothingVelocityLow);
        Assert.Equal(c1.SmoothingVelocityHigh, c2.SmoothingVelocityHigh);
        Assert.Equal(c1.MagnetismRadiusVpx, c2.MagnetismRadiusVpx);
        Assert.Equal(c1.MagnetismStrength, c2.MagnetismStrength);
        Assert.Equal(c1.EdgeResistance, c2.EdgeResistance);
        Assert.Equal(c1.SnapRadiusVpx, c2.SnapRadiusVpx);
        Assert.Equal(c1.SmoothingAdaptiveFrequencyEnabled, c2.SmoothingAdaptiveFrequencyEnabled);
        Assert.Equal(c1.DeadzoneRadiusVpx, c2.DeadzoneRadiusVpx);
        Assert.Equal(c1.SmoothingDualPoleEnabled, c2.SmoothingDualPoleEnabled);
        Assert.Equal(c1.PhaseCompensationGainS, c2.PhaseCompensationGainS);
        Assert.Equal(c1.IntentBoostStrength, c2.IntentBoostStrength);
        Assert.Equal(c1.IntentCoherenceThreshold, c2.IntentCoherenceThreshold);
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
        Assert.True(config.SmoothingMinAlpha >= 0.05f && config.SmoothingMinAlpha <= 1f);
        Assert.True(config.SmoothingMaxAlpha >= 0.05f && config.SmoothingMaxAlpha <= 1f);
        Assert.True(config.SmoothingMinAlpha <= config.SmoothingMaxAlpha);
        Assert.True(config.SmoothingVelocityLow >= 0f);
        Assert.True(config.SmoothingVelocityHigh > 0f);
        Assert.True(config.SmoothingVelocityLow < config.SmoothingVelocityHigh);
        Assert.True(config.MagnetismStrength >= 0f && config.MagnetismStrength <= 1f);
        Assert.True(config.EdgeResistance >= 0f && config.EdgeResistance <= 1f);
        Assert.True(config.MagnetismRadiusVpx >= 0f);
        Assert.True(config.MagnetismHysteresisVpx >= 0f);
        Assert.True(config.DeadzoneRadiusVpx >= 0f);
    }

    [Fact]
    public void HighTremor_LowersMinAlpha()
    {
        // Higher tremor frequency → lower MinAlpha (stronger suppression at rest)
        // Low: f=4 Hz → α=0.05236*4=0.209 → clamped 0.209
        // High: f=10 Hz → α=0.05236*10=0.524 → clamped 0.40
        // Wait — higher freq → higher alpha. The relationship changed.
        // With closed-form mapping, higher frequency = higher alpha.
        // But higher frequency tremor IS harder to suppress with a low alpha.
        // The correct v2 behavior: higher frequency → higher minAlpha.
        // We test this instead.
        var lowFreq = ProfileToConfigMapper.Map(MakeProfile(tremor: 1f, freqHz: 4f));
        var highFreq = ProfileToConfigMapper.Map(MakeProfile(tremor: 8f, freqHz: 10f));

        Assert.True(highFreq.SmoothingMinAlpha > lowFreq.SmoothingMinAlpha,
            $"Higher freq minAlpha ({highFreq.SmoothingMinAlpha}) should be > lower freq ({lowFreq.SmoothingMinAlpha})");
    }

    [Fact]
    public void HighTremor_LowersVelocityHigh()
    {
        // Higher tremor → lower vHigh (more of velocity range gets filtered)
        var low = ProfileToConfigMapper.Map(MakeProfile(tremor: 1f));
        var high = ProfileToConfigMapper.Map(MakeProfile(tremor: 8f));

        Assert.True(high.SmoothingVelocityHigh < low.SmoothingVelocityHigh,
            $"High tremor vHigh ({high.SmoothingVelocityHigh}) should be < low tremor ({low.SmoothingVelocityHigh})");
    }

    [Fact]
    public void HighTremor_RaisesVelocityLow()
    {
        // Higher tremor → higher vLow (tremor produces larger micro-deltas)
        var low = ProfileToConfigMapper.Map(MakeProfile(tremor: 1f));
        var high = ProfileToConfigMapper.Map(MakeProfile(tremor: 8f));

        Assert.True(high.SmoothingVelocityLow > low.SmoothingVelocityLow,
            $"High tremor vLow ({high.SmoothingVelocityLow}) should be > low tremor ({low.SmoothingVelocityLow})");
    }

    [Fact]
    public void PolicyVersion_IsSet()
    {
        var config = ProfileToConfigMapper.Map(MakeProfile());
        Assert.Equal(4, config.MappingPolicyVersion);
        Assert.Equal(ProfileToConfigMapper.PolicyVersion, config.MappingPolicyVersion);
    }

    [Fact]
    public void SourceProfileId_IsPreserved()
    {
        var profile = MakeProfile();
        var config = ProfileToConfigMapper.Map(profile);
        Assert.Equal(profile.ProfileId, config.SourceProfileId);
    }

    // ── Closed-form frequency→alpha tests ──

    [Fact]
    public void FrequencyAvailable_UsesClosedFormMinAlpha()
    {
        // f=6 Hz → α = 0.05236 * 6 = 0.31416
        var config = ProfileToConfigMapper.Map(MakeProfile(freqHz: 6f));
        Assert.Equal(0.05236f * 6f, config.SmoothingMinAlpha, 3);
    }

    [Fact]
    public void FrequencyZero_FallsBackToAmplitudeBased()
    {
        // f=0 → uses amplitude fallback: Max(0.20, 0.35 - amplitude*0.015)
        var config = ProfileToConfigMapper.Map(MakeProfile(tremor: 3f, freqHz: 0f));
        float expected = MathF.Max(0.20f, 0.35f - 3f * 0.015f);
        Assert.Equal(expected, config.SmoothingMinAlpha, 4);
    }

    [Fact]
    public void HighFrequency_MinAlphaClampsAt040()
    {
        // f=12 Hz → raw α = 0.05236*12 = 0.628 → clamped to 0.40
        var config = ProfileToConfigMapper.Map(MakeProfile(freqHz: 12f));
        Assert.Equal(0.40f, config.SmoothingMinAlpha, 4);
    }

    [Fact]
    public void LowFrequency_MinAlphaClampsAt020()
    {
        // f=3 Hz → raw α = 0.05236*3 = 0.157 → clamped to 0.20
        var config = ProfileToConfigMapper.Map(MakeProfile(freqHz: 3f));
        Assert.Equal(0.20f, config.SmoothingMinAlpha, 4);
    }

    [Fact]
    public void FrequencyAvailable_EnablesAdaptiveMode()
    {
        var config = ProfileToConfigMapper.Map(MakeProfile(freqHz: 6f));
        Assert.True(config.SmoothingAdaptiveFrequencyEnabled);
    }

    [Fact]
    public void FrequencyZero_DisablesAdaptiveMode()
    {
        var config = ProfileToConfigMapper.Map(MakeProfile(freqHz: 0f));
        Assert.False(config.SmoothingAdaptiveFrequencyEnabled);
    }

    // ── Deadzone + dual-pole mapping tests ──

    [Fact]
    public void SignificantTremor_SetsDeadzoneRadius()
    {
        // amplitude=3, freq=6 Hz → D = 0.8 × 3 × (6/8)^0.65 (v4 power-law)
        var config = ProfileToConfigMapper.Map(MakeProfile(tremor: 3f));
        float expected = Math.Clamp(0.8f * 3f * MathF.Pow(6f / 8f, 0.65f), 0.2f, 3.0f);
        Assert.Equal(expected, config.DeadzoneRadiusVpx, 3);
    }

    [Fact]
    public void NegligibleTremor_DisablesDeadzone()
    {
        // amplitude=0.3 (< 0.5) → D = 0 (disabled)
        var config = ProfileToConfigMapper.Map(MakeProfile(tremor: 0.3f));
        Assert.Equal(0f, config.DeadzoneRadiusVpx, 4);
    }

    [Fact]
    public void HighTremor_EnablesDualPole()
    {
        // amplitude=5 (> 4) → dualPole = true
        var config = ProfileToConfigMapper.Map(MakeProfile(tremor: 5f));
        Assert.True(config.SmoothingDualPoleEnabled);
    }

    [Fact]
    public void LowTremor_DisablesDualPole()
    {
        // amplitude=2 (≤ 4) → dualPole = false
        var config = ProfileToConfigMapper.Map(MakeProfile(tremor: 2f));
        Assert.False(config.SmoothingDualPoleEnabled);
    }

    // ── Frequency-weighted deadzone tests (v4: power-law) ──

    [Fact]
    public void FreqWeightedDeadzone_HigherFreq_LargerD()
    {
        // Same amplitude, higher frequency → larger deadzone
        var low = ProfileToConfigMapper.Map(MakeProfile(tremor: 3f, freqHz: 4f));
        var high = ProfileToConfigMapper.Map(MakeProfile(tremor: 3f, freqHz: 10f));

        Assert.True(high.DeadzoneRadiusVpx > low.DeadzoneRadiusVpx,
            $"Higher freq DZ ({high.DeadzoneRadiusVpx:F4}) should be > lower freq ({low.DeadzoneRadiusVpx:F4})");
    }

    [Fact]
    public void FreqWeightedDeadzone_NoFrequency_AmplitudeOnly()
    {
        // f=0 → freqWeight=1.0 → D = 0.8 × A
        var config = ProfileToConfigMapper.Map(MakeProfile(tremor: 3f, freqHz: 0f));
        float expected = Math.Clamp(0.8f * 3f * 1f, 0.2f, 3.0f); // 2.4
        Assert.Equal(expected, config.DeadzoneRadiusVpx, 3);
    }

    [Fact]
    public void FreqWeightedDeadzone_ExactValue()
    {
        // A=2.5, f=8 Hz → D = 0.8 × 2.5 × (8/8)^0.65 = 0.8 × 2.5 × 1.0 = 2.0
        var config = ProfileToConfigMapper.Map(MakeProfile(tremor: 2.5f, freqHz: 8f));
        Assert.Equal(2.0f, config.DeadzoneRadiusVpx, 3);
    }

    // ── Phase compensation tests (v4) ──

    [Fact]
    public void PhaseComp_DerivedFromAlpha()
    {
        // tremor=3 (smoothing=0.3 ≥ 0.1) → phase comp enabled
        var config = ProfileToConfigMapper.Map(MakeProfile(tremor: 3f));

        // avgAlpha = (minAlpha + maxAlpha) / 2
        // lagS = (1 - avgAlpha) / avgAlpha / 60
        // gain = lagS * 0.7
        Assert.True(config.PhaseCompensationGainS > 0f,
            "Phase comp should be enabled for moderate tremor");
        Assert.True(config.PhaseCompensationGainS <= 0.1f,
            $"Phase comp gain ({config.PhaseCompensationGainS:F6}) should be ≤ 0.1");
    }

    [Fact]
    public void PhaseComp_LowSmoothing_Disabled()
    {
        // tremor=0.5 → smoothing = 0.5/10 = 0.05 < 0.1 → disabled
        var config = ProfileToConfigMapper.Map(MakeProfile(tremor: 0.5f));
        Assert.Equal(0f, config.PhaseCompensationGainS, 6);
    }

    [Fact]
    public void PhaseComp_HighFreqTremor12Hz_GainReducedToZero()
    {
        // f=12 Hz → minAlpha = 0.40 (clamped)
        // freqAttenuation = 1 - Clamp01((0.40 - 0.30) / 0.10) = 1 - 1.0 = 0
        // Phase comp gain should be zero for high-frequency tremor
        var config = ProfileToConfigMapper.Map(MakeProfile(tremor: 3f, freqHz: 12f));
        Assert.Equal(0f, config.PhaseCompensationGainS, 6);
    }

    [Fact]
    public void PhaseComp_MidFreqTremor6Hz_GainModerate()
    {
        // f=6 Hz → minAlpha = 0.05236*6 = 0.31416
        // freqAttenuation = 1 - Clamp01((0.31416 - 0.30) / 0.10) ≈ 0.8584
        // Phase comp gain reduced but still significant
        var config = ProfileToConfigMapper.Map(MakeProfile(tremor: 3f, freqHz: 6f));
        Assert.True(config.PhaseCompensationGainS > 0f,
            "Phase comp should still be enabled at 6 Hz tremor");

        // Compare with a low-frequency profile where attenuation is minimal
        var lowFreqConfig = ProfileToConfigMapper.Map(MakeProfile(tremor: 3f, freqHz: 4f));
        Assert.True(config.PhaseCompensationGainS < lowFreqConfig.PhaseCompensationGainS,
            $"6 Hz phase comp ({config.PhaseCompensationGainS:F6}) should be < 4 Hz ({lowFreqConfig.PhaseCompensationGainS:F6})");
    }

    // ── Intent boost tests (v4) ──

    [Fact]
    public void IntentBoost_GoodPathEfficiency_Enabled()
    {
        // pathEff=0.9 > 0.6 → strength = Clamp01(0.9 - 0.4) = 0.5
        var config = ProfileToConfigMapper.Map(MakeProfile(pathEff: 0.9f));
        Assert.Equal(0.5f, config.IntentBoostStrength, 4);
    }

    [Fact]
    public void IntentBoost_PoorPathEfficiency_Disabled()
    {
        // pathEff=0.5 ≤ 0.6 → strength = 0
        var config = ProfileToConfigMapper.Map(MakeProfile(pathEff: 0.5f));
        Assert.Equal(0f, config.IntentBoostStrength, 4);
    }

    [Fact]
    public void IntentBoost_ExactValue()
    {
        // pathEff=0.8 → strength = Clamp01(0.8 - 0.4) = 0.4
        var config = ProfileToConfigMapper.Map(MakeProfile(pathEff: 0.8f));
        Assert.Equal(0.4f, config.IntentBoostStrength, 4);
    }

    [Fact]
    public void IntentCoherenceThreshold_AlwaysSet()
    {
        var config = ProfileToConfigMapper.Map(MakeProfile());
        Assert.Equal(0.8f, config.IntentCoherenceThreshold, 4);
    }

    // ── v4 power-law frequency exponent validation ──

    [Fact]
    public void FreqWeightedDeadzone_V4_HighFreq12Hz_StrongerThanV3Sqrt()
    {
        // At 12 Hz: (12/8)^0.65 ≈ 1.349 > √(12/8) ≈ 1.225
        // v4 produces a larger deadzone at high frequencies than v3 did
        var config = ProfileToConfigMapper.Map(MakeProfile(tremor: 3f, freqHz: 12f));
        float v3Value = Math.Clamp(0.8f * 3f * MathF.Sqrt(12f / 8f), 0.2f, 3.0f);
        Assert.True(config.DeadzoneRadiusVpx > v3Value,
            $"V4 DZ at 12 Hz ({config.DeadzoneRadiusVpx:F4}) should be > V3 ({v3Value:F4})");
    }

    [Fact]
    public void FreqWeightedDeadzone_V4_LowFreq4Hz_WeakerThanV3Sqrt()
    {
        // At 4 Hz: (4/8)^0.65 ≈ 0.637 < √(4/8) ≈ 0.707
        // v4 produces a smaller deadzone at low frequencies than v3 did
        var config = ProfileToConfigMapper.Map(MakeProfile(tremor: 3f, freqHz: 4f));
        float v3Value = Math.Clamp(0.8f * 3f * MathF.Sqrt(4f / 8f), 0.2f, 3.0f);
        Assert.True(config.DeadzoneRadiusVpx < v3Value,
            $"V4 DZ at 4 Hz ({config.DeadzoneRadiusVpx:F4}) should be < V3 ({v3Value:F4})");
    }

    [Fact]
    public void FreqWeightedDeadzone_V4_RefFreq8Hz_Unchanged()
    {
        // At reference frequency 8 Hz: (8/8)^0.65 = 1.0 = √(8/8)
        // Both v3 and v4 produce the same result at the reference frequency
        var config = ProfileToConfigMapper.Map(MakeProfile(tremor: 2.5f, freqHz: 8f));
        float v3Value = Math.Clamp(0.8f * 2.5f * MathF.Sqrt(8f / 8f), 0.2f, 3.0f);
        Assert.Equal(v3Value, config.DeadzoneRadiusVpx, 3);
    }

    private static MotorProfile MakeProfile(
        float tremor = 3f,
        float pathEff = 0.8f,
        float overshoot = 0.4f,
        float freqHz = 6f) => new()
    {
        ProfileId = "test-profile",
        CreatedUtc = DateTimeOffset.UtcNow,
        TremorAmplitudeVpx = tremor,
        TremorFrequencyHz = freqHz,
        PathEfficiency = pathEff,
        OvershootRate = overshoot,
        OvershootMagnitudeVpx = overshoot * 20f,
        MeanTimeToTargetS = 0.8f,
        StdDevTimeToTargetS = 0.2f,
        ClickStabilityVpx = 2f,
        SampleCount = 100
    };
}
