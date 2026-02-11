using CursorAssist.Engine.Core;
using CursorAssist.Engine.Metrics;
using Xunit;

namespace CursorAssist.Tests.Engine;

public class DeterministicPipelineTests
{
    [Fact]
    public void EmptyPipeline_PassesThrough()
    {
        var pipeline = new DeterministicPipeline(new TransformPipeline());
        var input = new InputSample(100f, 200f, 1f, 2f, false, false, 0);
        var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f };

        var result = pipeline.FixedStep(in input, ctx);

        Assert.Equal(100f, result.FinalCursor.X);
        Assert.Equal(200f, result.FinalCursor.Y);
    }

    [Fact]
    public void SameInputs_ProduceSameHash()
    {
        var p1 = new DeterministicPipeline(new TransformPipeline());
        var p2 = new DeterministicPipeline(new TransformPipeline());

        var input = new InputSample(50f, 75f, 0f, 0f, true, false, 0);
        var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f };

        var r1 = p1.FixedStep(in input, ctx);
        var r2 = p2.FixedStep(in input, ctx);

        Assert.Equal(r1.DeterminismHash, r2.DeterminismHash);
    }

    [Fact]
    public void DifferentInputs_ProduceDifferentHash()
    {
        var p1 = new DeterministicPipeline(new TransformPipeline());
        var p2 = new DeterministicPipeline(new TransformPipeline());

        var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f };

        var r1 = p1.FixedStep(new InputSample(50f, 75f, 0f, 0f, false, false, 0), ctx);
        var r2 = p2.FixedStep(new InputSample(51f, 75f, 0f, 0f, false, false, 0), ctx);

        Assert.NotEqual(r1.DeterminismHash, r2.DeterminismHash);
    }

    [Fact]
    public void HashAccumulates_AcrossMultipleTicks()
    {
        var pipeline = new DeterministicPipeline(new TransformPipeline());
        var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f };

        var r1 = pipeline.FixedStep(new InputSample(10f, 20f, 0f, 0f, false, false, 0), ctx);
        var hashAfterOne = r1.DeterminismHash;

        var ctx2 = new TransformContext { Tick = 1, Dt = 1f / 60f };
        var r2 = pipeline.FixedStep(new InputSample(30f, 40f, 0f, 0f, false, false, 1), ctx2);
        var hashAfterTwo = r2.DeterminismHash;

        Assert.NotEqual(hashAfterOne, hashAfterTwo);
    }

    [Fact]
    public void Reset_ClearsHash()
    {
        var pipeline = new DeterministicPipeline(new TransformPipeline());
        var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f };

        pipeline.FixedStep(new InputSample(10f, 20f, 0f, 0f, false, false, 0), ctx);
        pipeline.Reset();

        // After reset, hash should be back to offset basis
        var pipeline2 = new DeterministicPipeline(new TransformPipeline());
        Assert.Equal(pipeline2.DeterminismHash, pipeline.DeterminismHash);
    }

    [Fact]
    public void MetricsSink_ReceivesTicks()
    {
        var sink = new CountingSink();
        var pipeline = new DeterministicPipeline(new TransformPipeline(), sink);
        var ctx = new TransformContext { Tick = 0, Dt = 1f / 60f };

        pipeline.FixedStep(new InputSample(10f, 20f, 0f, 0f, false, false, 0), ctx);
        var ctx2 = new TransformContext { Tick = 1, Dt = 1f / 60f };
        pipeline.FixedStep(new InputSample(20f, 30f, 0f, 0f, false, false, 1), ctx2);

        Assert.Equal(2, sink.TickCount);
    }

    private sealed class CountingSink : IMetricsSink
    {
        public int TickCount { get; private set; }
        public void RecordTick(long tick, in InputSample raw, in InputSample transformed, IReadOnlyList<TargetInfo> targets) => TickCount++;
        public void RecordEvent(in EngineEvent engineEvent) { }
        public void Reset() => TickCount = 0;
    }
}
