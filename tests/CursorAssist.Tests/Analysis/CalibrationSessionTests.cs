using CursorAssist.Engine.Analysis;
using Xunit;

namespace CursorAssist.Tests.Analysis;

public class CalibrationSessionTests
{
    [Fact]
    public void SineInput_ProducesValidProfile()
    {
        var session = new CalibrationSession(durationTicks: 300);

        const float freq = 6f;
        const float amplitude = 3f;
        float prevX = 0f;

        for (int i = 0; i < 300; i++)
        {
            float phase = 2f * MathF.PI * freq * i / 60f;
            float x = amplitude * MathF.Sin(phase);
            float dx = x - prevX;
            session.RecordTick(dx, 0f);
            prevX = x;
        }

        Assert.True(session.IsComplete);

        var result = session.GetResult();
        Assert.InRange(result.FrequencyHz, 4f, 8f);
        Assert.True(result.AmplitudeVpx > 0f);
        Assert.Equal(300, result.SampleCount);

        // Convert to profile and verify conservative defaults
        var profile = result.ToMotorProfile("calibration-test");
        Assert.Equal("calibration-test", profile.ProfileId);
        Assert.Equal(0.7f, profile.PathEfficiency);
        Assert.Equal(0.3f, profile.OvershootRate);
        Assert.InRange(profile.TremorFrequencyHz, 4f, 8f);
    }

    [Fact]
    public void IsComplete_AfterDuration()
    {
        var session = new CalibrationSession(durationTicks: 100);

        for (int i = 0; i < 99; i++)
        {
            Assert.False(session.IsComplete);
            session.RecordTick(0.1f, 0f);
        }

        Assert.False(session.IsComplete);
        session.RecordTick(0.1f, 0f);
        Assert.True(session.IsComplete);

        // Recording after complete should be ignored
        session.RecordTick(0.1f, 0f);
        Assert.Equal(100, session.TickCount);
    }

    [Fact]
    public void ZeroInput_LowConfidence()
    {
        var session = new CalibrationSession(durationTicks: 300);

        for (int i = 0; i < 300; i++)
            session.RecordTick(0f, 0f);

        var result = session.GetResult();

        // No tremor detected → low confidence (≤ 0.3)
        Assert.True(result.Confidence <= 0.35f,
            $"Zero input should yield low confidence, got {result.Confidence:F2}");
    }

    [Fact]
    public void ToMotorProfile_ConservativeDefaults()
    {
        // Even with no meaningful input, the profile should have safe defaults
        var session = new CalibrationSession(durationTicks: 60);

        for (int i = 0; i < 60; i++)
            session.RecordTick(0f, 0f);

        var result = session.GetResult();
        var profile = result.ToMotorProfile("user-123");

        Assert.Equal("user-123", profile.ProfileId);
        Assert.Equal(0.7f, profile.PathEfficiency);
        Assert.Equal(0.3f, profile.OvershootRate);
        Assert.Equal(6f, profile.OvershootMagnitudeVpx); // 0.3 * 20
        Assert.Equal(0.8f, profile.MeanTimeToTargetS);
        Assert.Equal(2f, profile.ClickStabilityVpx);
    }
}
