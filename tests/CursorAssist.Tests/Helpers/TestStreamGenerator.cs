using CursorAssist.Canon.Schemas;
using CursorAssist.Engine.Core;
using CursorAssist.Engine.Transforms;
using CursorAssist.Runtime.Core;

namespace CursorAssist.Tests.Helpers;

/// <summary>
/// Shared deterministic stream generators and pipeline builders for stability
/// and regression tests. All generators are fully deterministic.
/// </summary>
internal static class TestStreamGenerator
{
    /// <summary>
    /// Generate a deterministic raw input stream using xorshift32.
    /// Simulates cursor movement with noise. Dx/Dy ∈ [−5, +5] vpx.
    /// Same algorithm as ReplayGoldenHashTests.GenerateDeterministicStream.
    /// </summary>
    public static List<RawInputEvent> GenerateDeterministicStream(int count, uint seed)
    {
        var events = new List<RawInputEvent>(count);
        uint state = seed == 0 ? 0xDEADBEEFu : seed;

        for (int i = 0; i < count; i++)
        {
            // xorshift32
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;

            float dx = ((state & 0xFF) / 255f - 0.5f) * 10f;
            float dy = (((state >> 8) & 0xFF) / 255f - 0.5f) * 10f;
            bool primary = (state & 0x10000) != 0;

            events.Add(new RawInputEvent(dx, dy, primary, false, i * 166667L));
        }

        return events;
    }

    /// <summary>
    /// Generate a zero-input stream: all deltas zero, buttons false.
    /// </summary>
    public static List<RawInputEvent> GenerateZeroStream(int count)
    {
        var events = new List<RawInputEvent>(count);
        for (int i = 0; i < count; i++)
        {
            events.Add(new RawInputEvent(0f, 0f, false, false, i * 166667L));
        }
        return events;
    }

    /// <summary>
    /// Generate a pure sinusoidal tremor stream on the X axis.
    /// Position: x(t) = A·sin(2πf·t/Fs). Delta: Dx[i] = x(i) − x(i−1).
    /// Dy = 0 throughout.
    /// </summary>
    public static List<RawInputEvent> GenerateSineStream(
        int count, float frequencyHz, float amplitudeVpx, float sampleRateHz = 60f)
    {
        var events = new List<RawInputEvent>(count);
        float prevX = 0f;

        for (int i = 0; i < count; i++)
        {
            float posX = amplitudeVpx * MathF.Sin(2f * MathF.PI * frequencyHz * i / sampleRateHz);
            float dx = posX - prevX;
            prevX = posX;
            events.Add(new RawInputEvent(dx, 0f, false, false, i * 166667L));
        }

        return events;
    }

    /// <summary>
    /// Generate a constant-velocity phase followed by a sudden stop.
    /// First <paramref name="moveTicks"/> events at constant Dx, then
    /// <paramref name="stopTicks"/> events at Dx=0.
    /// </summary>
    public static List<RawInputEvent> GenerateConstantVelocityThenStop(
        int moveTicks, int stopTicks, float dxPerTick)
    {
        var events = new List<RawInputEvent>(moveTicks + stopTicks);
        for (int i = 0; i < moveTicks; i++)
        {
            events.Add(new RawInputEvent(dxPerTick, 0f, false, false, i * 166667L));
        }
        for (int i = 0; i < stopTicks; i++)
        {
            events.Add(new RawInputEvent(0f, 0f, false, false, (moveTicks + i) * 166667L));
        }
        return events;
    }

    /// <summary>
    /// Build the full canonical transform pipeline:
    /// SoftDeadzone → Smoothing → PhaseCompensation → DirectionalIntent → Magnetism
    /// </summary>
    public static TransformPipeline BuildFullPipeline() =>
        new TransformPipeline()
            .Add(new SoftDeadzoneTransform())
            .Add(new SmoothingTransform())
            .Add(new PhaseCompensationTransform())
            .Add(new DirectionalIntentTransform())
            .Add(new TargetMagnetismTransform());

    /// <summary>
    /// Create a realistic mid-range AssistiveConfig with all transforms active.
    /// </summary>
    public static AssistiveConfig MakeRealisticConfig() => new()
    {
        SourceProfileId = "stability-test",
        SmoothingStrength = 0.6f,
        SmoothingMinAlpha = 0.25f,
        SmoothingMaxAlpha = 0.90f,
        SmoothingVelocityLow = 0.5f,
        SmoothingVelocityHigh = 8f,
        DeadzoneRadiusVpx = 1.5f,
        PhaseCompensationGainS = 0.008f,
        IntentBoostStrength = 0.5f,
        IntentCoherenceThreshold = 0.8f,
        MagnetismRadiusVpx = 80f,
        MagnetismStrength = 0.4f,
        MagnetismHysteresisVpx = 12f,
        SnapRadiusVpx = 3f
    };

    /// <summary>
    /// Create a full-featured AssistiveConfig with all Phase 4 features active:
    /// adaptive frequency, dual-pole, precision mode, intent hysteresis,
    /// phase comp velocity saturation (implicit in transform), and all transforms.
    /// Used by the risk matrix regression suite.
    /// </summary>
    public static AssistiveConfig MakeFullFeaturedConfig() => new()
    {
        SourceProfileId = "full-featured-test",
        SmoothingStrength = 0.7f,
        SmoothingMinAlpha = 0.25f,
        SmoothingMaxAlpha = 0.90f,
        SmoothingVelocityLow = 0.5f,
        SmoothingVelocityHigh = 8f,
        SmoothingAdaptiveFrequencyEnabled = true,
        SmoothingDualPoleEnabled = true,
        PrecisionModeEnabled = true,
        DeadzoneRadiusVpx = 1.5f,
        PhaseCompensationGainS = 0.008f,
        IntentBoostStrength = 0.5f,
        IntentCoherenceThreshold = 0.8f,
        IntentDisengageThreshold = 0.65f,
        MagnetismRadiusVpx = 80f,
        MagnetismStrength = 0.4f,
        MagnetismHysteresisVpx = 12f,
        SnapRadiusVpx = 3f
    };
}
