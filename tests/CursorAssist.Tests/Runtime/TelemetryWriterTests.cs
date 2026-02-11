using System.Text.Json;
using CursorAssist.Canon.Schemas;
using CursorAssist.Runtime.Core;
using CursorAssist.Trace;
using Xunit;

namespace CursorAssist.Tests.Runtime;

public class TelemetryWriterTests : IDisposable
{
    private readonly string _tempDir;

    public TelemetryWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ca-test-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static AssistiveConfig MakeTestConfig() => new()
    {
        SourceProfileId = "test-profile-001",
        SmoothingStrength = 0.5f,
        SmoothingMinAlpha = 0.25f,
        SmoothingMaxAlpha = 0.90f,
        DeadzoneRadiusVpx = 1.0f,
        PhaseCompensationGainS = 0.008f
    };

    [Fact]
    public void Begin_CreatesSessionDirectory()
    {
        using var writer = new TelemetryWriter();
        var config = MakeTestConfig();

        string sessionId = writer.Begin(_tempDir, config);

        // Session directory exists
        Assert.NotNull(writer.SessionDir);
        Assert.True(Directory.Exists(writer.SessionDir));

        // Session ID is non-empty and contained in directory name
        Assert.False(string.IsNullOrEmpty(sessionId));
        Assert.Contains(sessionId, writer.SessionDir);

        // config.json written and deserializable
        string configPath = Path.Combine(writer.SessionDir, "config.json");
        Assert.True(File.Exists(configPath));

        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var deserialized = JsonSerializer.Deserialize<AssistiveConfig>(
            File.ReadAllText(configPath), opts);

        Assert.NotNull(deserialized);
        Assert.Equal("test-profile-001", deserialized!.SourceProfileId);
        Assert.Equal(0.5f, deserialized.SmoothingStrength);
        Assert.Equal(1.0f, deserialized.DeadzoneRadiusVpx);
    }

    [Fact]
    public void End_WritesSummaryJson_Deserializable()
    {
        using var writer = new TelemetryWriter();
        var config = MakeTestConfig();
        writer.Begin(_tempDir, config);

        var now = DateTimeOffset.UtcNow;
        var summary = new SessionSummary
        {
            SessionId = writer.SessionId,
            StartedUtc = now.AddSeconds(-30),
            EndedUtc = now,
            DurationSeconds = 30f,
            ConfigHash = TelemetryWriter.ComputeConfigHash(config),
            FixedHz = 60,
            TotalTicks = 1800,
            OverrunCount = 2,
            MeanVelocity = 3.5f,
            PeakVelocity = 12.0f,
            EmergencyStopFired = false,
            ExitReason = "user"
        };

        writer.End(summary);

        // summary.json exists and is deserializable
        string summaryPath = Path.Combine(writer.SessionDir!, "summary.json");
        Assert.True(File.Exists(summaryPath));

        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var deserialized = JsonSerializer.Deserialize<SessionSummary>(
            File.ReadAllText(summaryPath), opts);

        Assert.NotNull(deserialized);
        Assert.Equal(writer.SessionId, deserialized!.SessionId);
        Assert.Equal(1800, deserialized.TotalTicks);
        Assert.Equal(2, deserialized.OverrunCount);
        Assert.Equal(3.5f, deserialized.MeanVelocity);
        Assert.Equal(12.0f, deserialized.PeakVelocity);
        Assert.False(deserialized.EmergencyStopFired);
        Assert.Equal("user", deserialized.ExitReason);

        // Config hash is stable
        string hash1 = TelemetryWriter.ComputeConfigHash(config);
        string hash2 = TelemetryWriter.ComputeConfigHash(config);
        Assert.Equal(hash1, hash2);
        Assert.Equal(16, hash1.Length); // 64-bit hex
    }

    [Fact]
    public void AttachTraceWriter_WritesValidTrace()
    {
        string? sessionDir;

        // Scope the writer so the trace file handle is released before reading
        using (var writer = new TelemetryWriter())
        {
            var config = MakeTestConfig();
            writer.Begin(_tempDir, config);
            sessionDir = writer.SessionDir;

            var header = new TraceHeader
            {
                SourceApp = "cursorassist-pilot",
                SourceVersion = "0.1.0",
                FixedHz = 60,
                RunId = writer.SessionId
            };

            var traceWriter = writer.AttachTraceWriter(header);
            Assert.NotNull(traceWriter);

            // Write a few samples
            for (int i = 0; i < 5; i++)
            {
                traceWriter!.WriteSample(new TraceSample
                {
                    Tick = i,
                    X = 100f + i * 5f,
                    Y = 200f,
                    Dx = 5f,
                    Dy = 0f,
                    Buttons = 0
                });
            }
        } // Dispose releases the trace file handle

        // Trace file exists with header + 5 lines = 6 lines
        string tracePath = Path.Combine(sessionDir!, "trace.castrace.jsonl");
        Assert.True(File.Exists(tracePath));

        string[] lines = File.ReadAllLines(tracePath);
        Assert.Equal(6, lines.Length); // 1 header + 5 samples

        // First line is header
        Assert.Contains("\"type\":\"header\"", lines[0]);
        Assert.Contains("cursorassist-pilot", lines[0]);

        // Remaining lines are tick samples
        for (int i = 1; i <= 5; i++)
        {
            Assert.Contains("\"type\":\"tick\"", lines[i]);
            Assert.Contains($"\"tick\":{i - 1}", lines[i]);
        }
    }
}
