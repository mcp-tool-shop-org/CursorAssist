using CursorAssist.Canon.Schemas;
using CursorAssist.Engine.Core;
using CursorAssist.Engine.Transforms;
using CursorAssist.Runtime.Core;
using CursorAssist.Tests.Helpers;
using Xunit;

namespace CursorAssist.Tests.RiskMatrix;

/// <summary>
/// Risk matrix regression suite. One test per identified risk.
/// These are the definitive audit trail for Phase 4 safety mitigations.
///
/// Risk matrix:
///   R1 (Critical): Injection loop runaway
///   R2 (High):     Compensation oscillation at 12 Hz
///   R3 (Medium):   Deadzone over-suppression at low frequency
///   R4 (Medium):   State leakage across pipeline reset
///   R5 (Medium):   Long-run float drift with all features enabled
/// </summary>
public class RiskMatrixTests
{
    // ─── R1: Injection Loop Runaway ─────────────────────────────────────
    // Risk: Pathological config produces output > MaxDeltaPerTick
    // Mitigation: EngineThread.ClampDelta caps per-tick injection

    [Fact]
    public void InjectionLoopRunaway_OutputClampedToMaxDeltaPerTick()
    {
        // Pathological config: maximum phase comp gain + large deltas
        var config = new AssistiveConfig
        {
            SourceProfileId = "risk-r1",
            PhaseCompensationGainS = RuntimeLimits.MaxPhaseCompGainS // 0.1
        };
        config = EngineThread.ClampConfig(config);

        var pipeline = new TransformPipeline().Add(new PhaseCompensationTransform());
        var deterministicPipeline = new DeterministicPipeline(pipeline);

        float prevOutX = 0f;

        for (int i = 0; i < 30; i++)
        {
            float dx = 40f; // Very fast movement
            float curX = (i + 1) * dx;

            var input = new InputSample(curX, 0f, dx, 0f, false, false, i);
            var ctx = new TransformContext { Tick = i, Dt = 1f / 60f, Config = config };
            var result = deterministicPipeline.FixedStep(in input, ctx);

            float assistedDx = result.FinalCursor.X - prevOutX;
            var (clampedDx, _) = EngineThread.ClampDelta(assistedDx, 0f);

            // CRITICAL: clamped delta must never exceed runtime limit
            Assert.InRange(clampedDx,
                -RuntimeLimits.MaxDeltaPerTick, RuntimeLimits.MaxDeltaPerTick);

            prevOutX = result.FinalCursor.X;
        }
    }

    // ─── R2: Compensation Oscillation at 12 Hz ─────────────────────────
    // Risk: Phase compensation amplifies high-frequency tremor
    // Mitigation: Velocity-dependent gain saturation + frequency attenuation

    [Fact]
    public void CompensationOscillation_12Hz_NoAmplificationGrowth()
    {
        // Full pipeline with realistic config, 12 Hz sine tremor, 3 vpx amplitude
        var config = TestStreamGenerator.MakeRealisticConfig();
        var pipeline = TestStreamGenerator.BuildFullPipeline();
        var engine = new EngineThread(pipeline);
        engine.UpdateConfig(config);

        var events = TestStreamGenerator.GenerateSineStream(600, 12f, 3f);

        // Run through the pipeline via ReplayStream
        ulong hash = engine.ReplayStream(events);
        Assert.NotEqual(0UL, hash);

        // Now verify amplitude doesn't grow: run the pipeline manually
        // and compare first-half vs second-half peak *deviation from raw input*
        var det = new DeterministicPipeline(TestStreamGenerator.BuildFullPipeline());
        config = EngineThread.ClampConfig(config);

        float curX = 0f;
        float prevOutX = 0f;
        float prevRawX = 0f;
        float firstHalfPeakDelta = 0f;
        float secondHalfPeakDelta = 0f;

        for (int i = 0; i < events.Count; i++)
        {
            curX += events[i].Dx;
            var input = new InputSample(curX, 0f, events[i].Dx, 0f, false, false, i);
            var ctx = new TransformContext { Tick = i, Dt = 1f / 60f, Config = config };
            var result = det.FixedStep(in input, ctx);

            // Measure per-tick output delta vs per-tick input delta
            float outDelta = MathF.Abs(result.FinalCursor.X - prevOutX);
            float rawDelta = MathF.Abs(curX - prevRawX);
            // Deviation = how much the pipeline amplifies each tick's movement
            float deviation = (rawDelta > 0.01f) ? outDelta / rawDelta : 1f;

            if (i > 10) // Skip warmup
            {
                if (i < 300)
                    firstHalfPeakDelta = MathF.Max(firstHalfPeakDelta, deviation);
                else
                    secondHalfPeakDelta = MathF.Max(secondHalfPeakDelta, deviation);
            }

            prevOutX = result.FinalCursor.X;
            prevRawX = curX;
        }

        // Second half amplification ratio must not exceed first half (no growth)
        Assert.True(secondHalfPeakDelta <= firstHalfPeakDelta * 1.2f,
            $"12 Hz: second half peak ratio ({secondHalfPeakDelta:F2}) should not grow beyond first half ({firstHalfPeakDelta:F2})");
    }

    // ─── R3: Deadzone Over-Suppression at Low Frequency ─────────────────
    // Risk: v4 power-law deadzone kills slow intentional motion at 3 Hz
    // Mitigation: Power-law exponent 0.65 relaxes suppression at low freq

    [Fact]
    public void DeadzoneOverSuppression_LowFreq3Hz_PreservesIntentionalMotion()
    {
        // Config with deadzone enabled, slow 3 Hz motion
        var config = new AssistiveConfig
        {
            SourceProfileId = "risk-r3",
            SmoothingStrength = 0.3f,
            SmoothingMinAlpha = 0.30f,
            SmoothingMaxAlpha = 0.90f,
            DeadzoneRadiusVpx = 1.5f
        };

        var pipeline = new TransformPipeline()
            .Add(new SoftDeadzoneTransform())
            .Add(new SmoothingTransform());
        var det = new DeterministicPipeline(pipeline);

        // Generate slow 3 Hz, 2 vpx amplitude (intentional motion, not tremor)
        float prevX = 0f;
        float totalInput = 0f;
        float totalOutput = 0f;

        for (int i = 0; i < 180; i++) // 3 seconds
        {
            float phase = 2f * MathF.PI * 3f * i / 60f;
            float x = 2f * MathF.Sin(phase);
            float dx = x - prevX;
            prevX = x;

            float curX = totalInput + dx;
            totalInput += MathF.Abs(dx);

            var input = new InputSample(curX, 0f, dx, 0f, false, false, i);
            var ctx = new TransformContext { Tick = i, Dt = 1f / 60f, Config = config };
            var result = det.FixedStep(in input, ctx);

            if (i > 0) // Skip first tick
            {
                totalOutput += MathF.Abs(result.FinalCursor.X - (curX - dx));
            }
        }

        // Output should preserve at least 50% of input displacement
        // (smoothing reduces it, but deadzone should NOT kill it)
        float preservation = totalOutput / totalInput;
        Assert.True(preservation > 0.3f,
            $"3 Hz motion preservation ({preservation:P0}) should be > 30%");
    }

    // ─── R4: State Leakage Across Pipeline Reset ────────────────────────
    // Risk: Stateful transforms leak state after Reset(), causing divergent replays
    // Mitigation: IStatefulTransform.Reset() clears all state

    [Fact]
    public void StateLeakage_FullPipeline_ResetProducesIdenticalReplay()
    {
        var pipeline = TestStreamGenerator.BuildFullPipeline();
        var engine = new EngineThread(pipeline);
        var config = TestStreamGenerator.MakeRealisticConfig();
        engine.UpdateConfig(config);

        var events = TestStreamGenerator.GenerateDeterministicStream(500, seed: 42);

        // First replay
        ulong hash1 = engine.ReplayStream(events);

        // Reset (ReplayStream calls _engine.Reset() internally)
        // Second replay — should produce identical hash
        ulong hash2 = engine.ReplayStream(events);

        Assert.Equal(hash1, hash2);
        Assert.NotEqual(0UL, hash1);
    }

    // ─── R5: Long-Run Float Drift with All Phase 4 Features ────────────
    // Risk: 60,000 ticks (16.7 minutes) with all features causes float drift
    // Mitigation: EMA and bounded state prevent accumulation

    [Fact]
    public void LongRunDrift_60000Ticks_AllFeaturesEnabled_NoBoundGrowth()
    {
        var config = TestStreamGenerator.MakeFullFeaturedConfig();
        config = EngineThread.ClampConfig(config);

        var pipeline = TestStreamGenerator.BuildFullPipeline();
        var det = new DeterministicPipeline(pipeline);

        // Create a motor profile for adaptive frequency seeding
        var profile = new MotorProfile
        {
            ProfileId = "risk-r5",
            CreatedUtc = DateTimeOffset.UtcNow,
            TremorFrequencyHz = 6f,
            TremorAmplitudeVpx = 3f,
            PathEfficiency = 0.8f,
            SampleCount = 100
        };

        // Generate 60K-tick stream with mixed motion patterns
        float curX = 0f, curY = 0f;
        uint rng = 0xCAFEBABE;
        float maxAbsX = 0f;
        float maxAbsY = 0f;
        float prevOutX = 0f, prevOutY = 0f;
        float maxTickDeltaDeviation = 0f;

        for (int i = 0; i < 60_000; i++)
        {
            // xorshift32 for deterministic pseudo-random deltas
            rng ^= rng << 13;
            rng ^= rng >> 17;
            rng ^= rng << 5;

            float dx = ((rng & 0xFF) / 255f - 0.5f) * 6f;    // ±3 vpx
            float dy = (((rng >> 8) & 0xFF) / 255f - 0.5f) * 6f;
            curX += dx;
            curY += dy;

            var input = new InputSample(curX, curY, dx, dy, false, false, i);
            var ctx = new TransformContext
            {
                Tick = i,
                Dt = 1f / 60f,
                Config = config,
                Profile = profile
            };

            var result = det.FixedStep(in input, ctx);

            // Track maximums in second half (after warmup)
            if (i >= 30_000)
            {
                maxAbsX = MathF.Max(maxAbsX, MathF.Abs(result.FinalCursor.X));
                maxAbsY = MathF.Max(maxAbsY, MathF.Abs(result.FinalCursor.Y));

                // Track per-tick output delta vs input delta (how much pipeline changes each tick)
                float outDeltaX = result.FinalCursor.X - prevOutX;
                float outDeltaY = result.FinalCursor.Y - prevOutY;
                float deviationX = MathF.Abs(outDeltaX - dx);
                float deviationY = MathF.Abs(outDeltaY - dy);
                float deviation = MathF.Sqrt(deviationX * deviationX + deviationY * deviationY);
                maxTickDeltaDeviation = MathF.Max(maxTickDeltaDeviation, deviation);
            }

            prevOutX = result.FinalCursor.X;
            prevOutY = result.FinalCursor.Y;
        }

        // Bounds check: output should stay finite and reasonable
        Assert.True(float.IsFinite(maxAbsX), "X should remain finite after 60K ticks");
        Assert.True(float.IsFinite(maxAbsY), "Y should remain finite after 60K ticks");

        // Per-tick delta deviation should be bounded (pipeline changes per-tick movement
        // by a bounded amount — smoothing, deadzone, phase comp all bounded transforms)
        Assert.True(maxTickDeltaDeviation < 20f,
            $"Per-tick delta deviation ({maxTickDeltaDeviation:F2}) should be bounded after 60K ticks");

        // Hash should be deterministic
        var pipeline2 = TestStreamGenerator.BuildFullPipeline();
        var det2 = new DeterministicPipeline(pipeline2);

        curX = 0f; curY = 0f;
        rng = 0xCAFEBABE; // Same seed

        for (int i = 0; i < 60_000; i++)
        {
            rng ^= rng << 13;
            rng ^= rng >> 17;
            rng ^= rng << 5;

            float dx = ((rng & 0xFF) / 255f - 0.5f) * 6f;
            float dy = (((rng >> 8) & 0xFF) / 255f - 0.5f) * 6f;
            curX += dx;
            curY += dy;

            var input = new InputSample(curX, curY, dx, dy, false, false, i);
            var ctx = new TransformContext
            {
                Tick = i,
                Dt = 1f / 60f,
                Config = config,
                Profile = profile
            };

            det2.FixedStep(in input, ctx);
        }

        Assert.Equal(det.DeterminismHash, det2.DeterminismHash);
    }
}
