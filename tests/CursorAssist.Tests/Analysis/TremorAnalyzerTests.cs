using CursorAssist.Engine.Analysis;
using Xunit;

namespace CursorAssist.Tests.Analysis;

public class TremorAnalyzerTests
{
    [Fact]
    public void PureSine_6Hz_DetectsFrequency()
    {
        var analyzer = new TremorAnalyzer();
        analyzer.Initialize(0f);

        // Feed 3 seconds (180 ticks) of 6 Hz sinusoidal tremor
        const float freq = 6f;
        const float amplitude = 3f;
        float prevX = 0f;

        for (int i = 0; i < 180; i++)
        {
            float phase = 2f * MathF.PI * freq * i / 60f;
            float x = amplitude * MathF.Sin(phase);
            float dx = x - prevX;
            float velocity = MathF.Abs(dx);
            analyzer.Update(dx, 0f, velocity);
            prevX = x;
        }

        // Should detect ~6 Hz (within 1 Hz tolerance)
        Assert.InRange(analyzer.FrequencyHz, 5f, 7f);
    }

    [Fact]
    public void PureSine_10Hz_DetectsFrequency()
    {
        var analyzer = new TremorAnalyzer();
        analyzer.Initialize(0f);

        const float freq = 10f;
        const float amplitude = 2f;
        float prevX = 0f;

        for (int i = 0; i < 180; i++)
        {
            float phase = 2f * MathF.PI * freq * i / 60f;
            float x = amplitude * MathF.Sin(phase);
            float dx = x - prevX;
            float velocity = MathF.Abs(dx);
            analyzer.Update(dx, 0f, velocity);
            prevX = x;
        }

        // Should detect ~10 Hz (within 1.5 Hz tolerance — EMA smoothing may lag)
        Assert.InRange(analyzer.FrequencyHz, 8.5f, 11.5f);
    }

    [Fact]
    public void PureSine_MeasuresAmplitude()
    {
        var analyzer = new TremorAnalyzer();
        analyzer.Initialize(0f);

        const float freq = 6f;
        const float amplitude = 4f; // Peak amplitude
        float prevX = 0f;

        for (int i = 0; i < 180; i++)
        {
            float phase = 2f * MathF.PI * freq * i / 60f;
            float x = amplitude * MathF.Sin(phase);
            float dx = x - prevX;
            float velocity = MathF.Abs(dx);
            analyzer.Update(dx, 0f, velocity);
            prevX = x;
        }

        // RMS of high-pass filtered sine deltas. The HP filter removes the slow
        // component, so RMS should be correlated with the delta amplitude.
        // We just verify it's nonzero and reasonable (within order of magnitude).
        Assert.True(analyzer.AmplitudeVpx > 0.1f,
            $"Amplitude should be measurably nonzero, got {analyzer.AmplitudeVpx:F4}");
        Assert.True(analyzer.AmplitudeVpx < 10f,
            $"Amplitude should be reasonable, got {analyzer.AmplitudeVpx:F4}");
    }

    [Fact]
    public void ZeroInput_NoFrequency()
    {
        var analyzer = new TremorAnalyzer();
        analyzer.Initialize(0f);

        // Feed 180 ticks of zero motion
        for (int i = 0; i < 180; i++)
        {
            analyzer.Update(0f, 0f, 0f);
        }

        // No tremor → frequency should stay at 0 (below 3 Hz minimum valid tremor band)
        Assert.True(analyzer.FrequencyHz < 3f,
            $"Zero input should yield no frequency detection, got {analyzer.FrequencyHz:F2} Hz");
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var analyzer = new TremorAnalyzer();
        analyzer.Initialize(0f);

        // Build up state with tremor
        const float freq = 6f;
        float prevX = 0f;
        for (int i = 0; i < 120; i++)
        {
            float phase = 2f * MathF.PI * freq * i / 60f;
            float x = 3f * MathF.Sin(phase);
            float dx = x - prevX;
            analyzer.Update(dx, 0f, MathF.Abs(dx));
            prevX = x;
        }

        // Should have detected frequency
        Assert.True(analyzer.FrequencyHz > 0f, "Should have frequency before reset");

        // Reset
        analyzer.Reset();

        // After reset, frequency should be 0
        Assert.Equal(0f, analyzer.FrequencyHz);
        Assert.Equal(0f, analyzer.AmplitudeVpx);
        Assert.Equal(0, analyzer.TotalTicks);
    }
}
