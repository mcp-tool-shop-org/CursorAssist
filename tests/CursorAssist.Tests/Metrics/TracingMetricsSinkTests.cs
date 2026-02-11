using CursorAssist.Engine.Core;
using CursorAssist.Engine.Metrics;
using CursorAssist.Trace;
using Xunit;

namespace CursorAssist.Tests.Metrics;

public class TracingMetricsSinkTests : IDisposable
{
    private readonly MemoryStream _stream;
    private readonly TraceWriter _traceWriter;
    private readonly TracingMetricsSink _sink;

    public TracingMetricsSinkTests()
    {
        _stream = new MemoryStream();
        _traceWriter = new TraceWriter(_stream);
        _traceWriter.WriteHeader(new TraceHeader
        {
            SourceApp = "test",
            FixedHz = 60
        });
        _sink = new TracingMetricsSink(_traceWriter);
    }

    public void Dispose()
    {
        _traceWriter.Dispose();
        _stream.Dispose();
    }

    [Fact]
    public void RecordTick_WritesToTraceWriter()
    {
        var raw = new InputSample(100f, 200f, 3f, 4f, false, false, 0);
        var transformed = new InputSample(101f, 201f, 2.5f, 3.5f, false, false, 0);

        _sink.RecordTick(0, in raw, in transformed, []);
        _sink.RecordTick(1, in raw, in transformed, []);
        _sink.RecordTick(2, in raw, in transformed, []);

        _traceWriter.Flush();

        // Read the stream contents
        _stream.Position = 0;
        using var reader = new StreamReader(_stream);
        string content = reader.ReadToEnd();
        string[] lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // 1 header + 3 tick samples
        Assert.Equal(4, lines.Length);

        // First line is header
        Assert.Contains("\"type\":\"header\"", lines[0]);

        // Remaining are tick samples with transformed values
        for (int i = 1; i <= 3; i++)
        {
            Assert.Contains("\"type\":\"tick\"", lines[i]);
            Assert.Contains($"\"tick\":{i - 1}", lines[i]);
        }
    }

    [Fact]
    public void ExportStats_CorrectMeanVelocity()
    {
        // Dx=3, Dy=4 → velocity magnitude = 5.0
        var raw = new InputSample(0f, 0f, 3f, 4f, false, false, 0);
        var transformed = new InputSample(0f, 0f, 3f, 4f, false, false, 0);

        _sink.RecordTick(0, in raw, in transformed, []);
        _sink.RecordTick(1, in raw, in transformed, []);
        _sink.RecordTick(2, in raw, in transformed, []);

        var stats = _sink.ExportStats();

        Assert.Equal(3, stats.TotalTicks);
        Assert.Equal(5.0f, stats.MeanVelocity, precision: 3);
        Assert.Equal(5.0f, stats.PeakVelocity, precision: 3);
    }

    [Fact]
    public void ExportStats_TracksVaryingVelocity()
    {
        // Tick 0: vel = sqrt(1+0) = 1.0
        var t0 = new InputSample(0f, 0f, 1f, 0f, false, false, 0);
        // Tick 1: vel = sqrt(9+16) = 5.0
        var t1 = new InputSample(0f, 0f, 3f, 4f, false, false, 1);
        // Tick 2: vel = sqrt(0+9) = 3.0
        var t2 = new InputSample(0f, 0f, 0f, 3f, false, false, 2);

        _sink.RecordTick(0, in t0, in t0, []);
        _sink.RecordTick(1, in t1, in t1, []);
        _sink.RecordTick(2, in t2, in t2, []);

        var stats = _sink.ExportStats();

        Assert.Equal(3, stats.TotalTicks);
        Assert.Equal(5.0f, stats.PeakVelocity, precision: 3);
        // Mean = (1 + 5 + 3) / 3 = 3.0
        Assert.Equal(3.0f, stats.MeanVelocity, precision: 3);
    }

    [Fact]
    public void Reset_ClearsStats()
    {
        var raw = new InputSample(0f, 0f, 3f, 4f, false, false, 0);
        _sink.RecordTick(0, in raw, in raw, []);
        _sink.RecordTick(1, in raw, in raw, []);

        _sink.Reset();

        var stats = _sink.ExportStats();
        Assert.Equal(0, stats.TotalTicks);
        Assert.Equal(0f, stats.MeanVelocity);
        Assert.Equal(0f, stats.PeakVelocity);
    }
}
