using CursorAssist.Canon.Schemas;

namespace CursorAssist.Engine.Analysis;

/// <summary>
/// Result of a calibration session. Contains estimated tremor parameters
/// and a method to produce a conservative MotorProfile.
///
/// Confidence is based on the number of ticks processed and whether a
/// valid tremor frequency was detected.
/// </summary>
public readonly record struct CalibrationResult(
    float FrequencyHz,
    float AmplitudeVpx,
    float Confidence,
    int SampleCount)
{
    /// <summary>
    /// Convert to a MotorProfile with conservative defaults for unmeasured fields.
    /// PathEfficiency=0.7 and OvershootRate=0.3 are safe middle-ground values
    /// that avoid over-aggressive assistance.
    /// </summary>
    public MotorProfile ToMotorProfile(string profileId) => new()
    {
        ProfileId = profileId,
        CreatedUtc = DateTimeOffset.UtcNow,
        TremorFrequencyHz = FrequencyHz,
        TremorAmplitudeVpx = AmplitudeVpx,
        PathEfficiency = 0.7f,       // Conservative default
        OvershootRate = 0.3f,        // Conservative default
        OvershootMagnitudeVpx = 0.3f * 20f, // 6 vpx
        MeanTimeToTargetS = 0.8f,
        StdDevTimeToTargetS = 0.2f,
        ClickStabilityVpx = 2f,
        SampleCount = SampleCount
    };
}
