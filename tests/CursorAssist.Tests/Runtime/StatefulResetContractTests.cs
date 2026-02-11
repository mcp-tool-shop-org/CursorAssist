using CursorAssist.Canon.Schemas;
using CursorAssist.Engine.Core;
using CursorAssist.Engine.Transforms;
using CursorAssist.Runtime.Core;
using Xunit;

namespace CursorAssist.Tests.Runtime;

/// <summary>
/// Validates the stateful transform reset contract:
/// 1. All stateful transforms implement IStatefulTransform
/// 2. After Reset(), replaying the same stream produces identical deterministic hash
/// </summary>
public class StatefulResetContractTests
{
    [Fact]
    public void AllStatefulTransforms_ImplementMarkerInterface()
    {
        Assert.IsAssignableFrom<IStatefulTransform>(new SoftDeadzoneTransform());
        Assert.IsAssignableFrom<IStatefulTransform>(new SmoothingTransform());
        Assert.IsAssignableFrom<IStatefulTransform>(new TargetMagnetismTransform());
        Assert.IsAssignableFrom<IStatefulTransform>(new DirectionalIntentTransform());

        // PhaseCompensation is stateless — must NOT implement IStatefulTransform
        // Use runtime type check to avoid CS0184 compile-time warning
        IInputTransform phaseComp = new PhaseCompensationTransform();
        Assert.False(phaseComp is IStatefulTransform,
            "PhaseCompensationTransform is stateless and should not implement IStatefulTransform");
    }

    [Fact]
    public void BackToBackReplay_SamePipelineInstance_IdenticalHashes()
    {
        // Build a single pipeline with all 5 transforms.
        // After first replay, transforms have stale state.
        // ReplayStream() calls _engine.Reset() which calls _transforms.Reset().
        // If Reset() works correctly, second replay produces identical hash.
        var pipeline = new TransformPipeline()
            .Add(new SoftDeadzoneTransform())
            .Add(new SmoothingTransform())
            .Add(new PhaseCompensationTransform())
            .Add(new DirectionalIntentTransform())
            .Add(new TargetMagnetismTransform());

        var config = new AssistiveConfig
        {
            SourceProfileId = "reset-test",
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

        var events = GenerateDeterministicStream(300, seed: 42);

        // Run 1: first use of the pipeline
        var engine1 = new EngineThread(pipeline);
        engine1.UpdateConfig(config);
        ulong hash1 = engine1.ReplayStream(events);

        // Run 2: REUSE the same pipeline object (transforms have stale state from run 1)
        // EngineThread wraps a new DeterministicPipeline, but the TransformPipeline
        // is shared. ReplayStream → Reset → TransformPipeline.Reset → each transform.Reset()
        var engine2 = new EngineThread(pipeline);
        engine2.UpdateConfig(config);
        ulong hash2 = engine2.ReplayStream(events);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void BackToBackReplay_DifferentConfig_DifferentHash()
    {
        var pipeline = new TransformPipeline()
            .Add(new SoftDeadzoneTransform())
            .Add(new SmoothingTransform());

        var events = GenerateDeterministicStream(300, seed: 42);

        var config1 = new AssistiveConfig
        {
            SourceProfileId = "reset-test",
            SmoothingStrength = 0.6f,
            DeadzoneRadiusVpx = 1.5f
        };

        var config2 = new AssistiveConfig
        {
            SourceProfileId = "reset-test",
            SmoothingStrength = 0.2f,
            DeadzoneRadiusVpx = 0.5f
        };

        var engine1 = new EngineThread(pipeline);
        engine1.UpdateConfig(config1);
        ulong hash1 = engine1.ReplayStream(events);

        var engine2 = new EngineThread(pipeline);
        engine2.UpdateConfig(config2);
        ulong hash2 = engine2.ReplayStream(events);

        Assert.NotEqual(hash1, hash2);
    }

    /// <summary>
    /// Generate a deterministic raw input stream using xorshift32.
    /// Same pattern as ReplayGoldenHashTests.
    /// </summary>
    private static List<RawInputEvent> GenerateDeterministicStream(int count, uint seed)
    {
        var events = new List<RawInputEvent>(count);
        uint state = seed == 0 ? 0xDEADBEEFu : seed;

        for (int i = 0; i < count; i++)
        {
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
}
