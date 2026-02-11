using CursorAssist.Trace;
using Xunit;

namespace CursorAssist.Tests.Trace;

public class TraceRoundTripTests
{
    [Fact]
    public void WriteAndRead_RoundTrips()
    {
        using var ms = new MemoryStream();

        // Write
        using (var writer = new TraceWriter(ms))
        {
            writer.WriteHeader(new TraceHeader
            {
                SourceApp = "test",
                FixedHz = 60,
                RunSeed = 0xC0FFEEu
            });

            for (int i = 0; i < 100; i++)
            {
                writer.WriteSample(new TraceSample
                {
                    Tick = i,
                    X = i * 10f,
                    Y = i * 5f,
                    Dx = 10f,
                    Dy = 5f,
                    Buttons = (byte)(i % 3 == 0 ? 1 : 0)
                });
            }

            writer.Flush();
        }

        // Read
        ms.Position = 0;
        using var reader = new TraceReader(ms);
        var header = reader.ReadHeader();

        Assert.Equal("test", header.SourceApp);
        Assert.Equal(60, header.FixedHz);
        Assert.Equal(0xC0FFEEu, header.RunSeed);

        var samples = reader.ReadSamples().ToList();
        Assert.Equal(100, samples.Count);
        Assert.Equal(0, samples[0].Tick);
        Assert.Equal(50f, samples[5].X, 0.01f);
        Assert.Equal(25f, samples[5].Y, 0.01f);
    }

    [Fact]
    public void Header_RequiresSourceApp()
    {
        using var ms = new MemoryStream();
        using var writer = new TraceWriter(ms);

        // Should write without error — SourceApp is required by init
        writer.WriteHeader(new TraceHeader { SourceApp = "unit-test" });
        writer.Flush();

        ms.Position = 0;
        using var reader = new TraceReader(ms);
        var header = reader.ReadHeader();
        Assert.Equal("unit-test", header.SourceApp);
    }
}
