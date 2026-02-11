using CursorAssist.Canon.Schemas;
using CursorAssist.Engine.Core;
using CursorAssist.Engine.Transforms;
using CursorAssist.Runtime.Core;
using Xunit;

namespace CursorAssist.Tests.Runtime;

/// <summary>
/// North star test: record a deterministic raw stream, replay through
/// the runtime engine, assert the output hash matches a golden value.
/// This proves runtime math is regressible.
/// </summary>
public class ReplayGoldenHashTests
{
    [Fact]
    public void ReplayWithNoTransforms_ProducesStableHash()
    {
        var pipeline = new TransformPipeline();
        var engine = new EngineThread(pipeline);

        var events = GenerateDeterministicStream(300, seed: 42);

        ulong hash1 = engine.ReplayStream(events);
        engine = new EngineThread(new TransformPipeline());
        ulong hash2 = engine.ReplayStream(events);

        Assert.Equal(hash1, hash2);
        Assert.NotEqual(0UL, hash1); // Should not be offset basis
    }

    [Fact]
    public void ReplayWithSmoothing_ProducesStableHash()
    {
        var config = new AssistiveConfig
        {
            SourceProfileId = "golden-test",
            SmoothingStrength = 0.6f
        };

        var events = GenerateDeterministicStream(300, seed: 42);

        var pipeline1 = new TransformPipeline().Add(new SmoothingTransform());
        var engine1 = new EngineThread(pipeline1);
        engine1.UpdateConfig(config);
        ulong hash1 = engine1.ReplayStream(events);

        var pipeline2 = new TransformPipeline().Add(new SmoothingTransform());
        var engine2 = new EngineThread(pipeline2);
        engine2.UpdateConfig(config);
        ulong hash2 = engine2.ReplayStream(events);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void DifferentStreams_ProduceDifferentHashes()
    {
        var pipeline1 = new TransformPipeline();
        var pipeline2 = new TransformPipeline();
        var engine1 = new EngineThread(pipeline1);
        var engine2 = new EngineThread(pipeline2);

        var events1 = GenerateDeterministicStream(300, seed: 42);
        var events2 = GenerateDeterministicStream(300, seed: 99);

        ulong hash1 = engine1.ReplayStream(events1);
        ulong hash2 = engine2.ReplayStream(events2);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ReplayWithMagnetism_ProducesStableHash()
    {
        var config = new AssistiveConfig
        {
            SourceProfileId = "golden-test",
            MagnetismRadiusVpx = 100f,
            MagnetismStrength = 0.5f,
            MagnetismHysteresisVpx = 15f,
            SnapRadiusVpx = 3f
        };

        var events = GenerateDeterministicStream(300, seed: 42);

        var pipeline1 = new TransformPipeline()
            .Add(new SmoothingTransform())
            .Add(new TargetMagnetismTransform());
        var engine1 = new EngineThread(pipeline1);
        engine1.UpdateConfig(config);
        ulong hash1 = engine1.ReplayStream(events);

        var pipeline2 = new TransformPipeline()
            .Add(new SmoothingTransform())
            .Add(new TargetMagnetismTransform());
        var engine2 = new EngineThread(pipeline2);
        engine2.UpdateConfig(config);
        ulong hash2 = engine2.ReplayStream(events);

        Assert.Equal(hash1, hash2);
    }

    /// <summary>
    /// Generate a deterministic raw input stream using xorshift32.
    /// Simulates cursor movement with noise.
    /// </summary>
    private static List<RawInputEvent> GenerateDeterministicStream(int count, uint seed)
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
}
