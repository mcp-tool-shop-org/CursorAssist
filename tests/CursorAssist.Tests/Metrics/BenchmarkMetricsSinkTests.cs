using CursorAssist.Engine.Core;
using CursorAssist.Engine.Metrics;
using Xunit;

namespace CursorAssist.Tests.Metrics;

public class BenchmarkMetricsSinkTests
{
    private static readonly TargetInfo Target = new("target-1", 500f, 300f, 60f, 40f);

    [Fact]
    public void EmptyTrials_ReturnsEmptySummary()
    {
        var sink = new BenchmarkMetricsSink();
        var summary = sink.ComputeSummary();
        Assert.Equal(0, summary.TrialCount);
    }

    [Fact]
    public void SingleHit_RecordsCorrectly()
    {
        var sink = new BenchmarkMetricsSink();
        sink.BeginTrial(Target, 200f, 300f);

        // Simulate ticks moving toward target
        for (int t = 0; t < 30; t++)
        {
            float x = 200f + (500f - 200f) * t / 30f;
            var sample = new InputSample(x, 300f, 0f, 0f, t == 29, false, t);
            sink.RecordTick(t, in sample, in sample, [Target]);
        }

        sink.EndTrial(true, 29);

        var summary = sink.ComputeSummary();
        Assert.Equal(1, summary.TrialCount);
        Assert.Equal(1, summary.HitCount);
        Assert.Equal(0f, summary.ErrorRate);
        Assert.True(summary.MeanTimeToTargetS > 0f);
    }

    [Fact]
    public void Miss_IncreasesErrorRate()
    {
        var sink = new BenchmarkMetricsSink();

        // Trial 1: hit
        sink.BeginTrial(Target, 200f, 300f);
        var hit = new InputSample(500f, 300f, 0f, 0f, true, false, 0);
        sink.RecordTick(0, in hit, in hit, [Target]);
        sink.EndTrial(true, 10);

        // Trial 2: miss
        sink.BeginTrial(Target, 200f, 300f);
        var miss = new InputSample(200f, 300f, 0f, 0f, false, false, 10);
        sink.RecordTick(10, in miss, in miss, [Target]);
        sink.EndTrial(false, 100);

        var summary = sink.ComputeSummary();
        Assert.Equal(2, summary.TrialCount);
        Assert.Equal(0.5f, summary.ErrorRate);
    }

    [Fact]
    public void PathEfficiency_StraightLineIsOptimal()
    {
        var sink = new BenchmarkMetricsSink();
        sink.BeginTrial(Target, 200f, 300f);

        // Perfect straight line from (200,300) to (500,300)
        for (int t = 0; t < 60; t++)
        {
            float x = 200f + 300f * t / 59f;
            var sample = new InputSample(x, 300f, 5f, 0f, t == 59, false, t);
            sink.RecordTick(t, in sample, in sample, [Target]);
        }
        sink.EndTrial(true, 59);

        var summary = sink.ComputeSummary();
        // Path efficiency should be close to 1.0 for straight line
        Assert.True(summary.MeanPathEfficiency > 0.95f, $"Expected near-perfect path efficiency, got {summary.MeanPathEfficiency}");
    }

    [Fact]
    public void Reset_ClearsTrials()
    {
        var sink = new BenchmarkMetricsSink();
        sink.BeginTrial(Target, 200f, 300f);
        sink.EndTrial(true, 10);

        sink.Reset();

        var summary = sink.ComputeSummary();
        Assert.Equal(0, summary.TrialCount);
    }
}
