using CursorAssist.Trace;
using MouseTrainer.Domain.Input;

namespace MouseTrainer.MauiHost;

/// <summary>
/// Exports per-tick cursor data as a .castrace.jsonl file during a session.
/// Neutral format — no CursorAssist.Engine dependency.
/// Call RecordTick() from the main loop, Complete() at session end.
/// </summary>
public sealed class TraceExporter : IDisposable
{
    private readonly TraceWriter _writer;
    private float _prevX, _prevY;
    private bool _hasPrev;
    private bool _disposed;

    public TraceExporter(string outputPath, uint? seed = null, string? runId = null)
    {
        _writer = new TraceWriter(outputPath);
        _writer.WriteHeader(new TraceHeader
        {
            SourceApp = "MouseTrainer.MauiHost",
            SourceVersion = "0.1.0",
            FixedHz = 60,
            RunSeed = seed,
            RunId = runId,
            VirtualWidth = 1920f,
            VirtualHeight = 1080f
        });
    }

    /// <summary>
    /// Record one tick's pointer input.
    /// </summary>
    public void RecordTick(int tick, in PointerInput input)
    {
        float dx = _hasPrev ? input.X - _prevX : 0f;
        float dy = _hasPrev ? input.Y - _prevY : 0f;

        byte buttons = 0;
        if (input.PrimaryDown) buttons |= 0x01;
        if (input.SecondaryDown) buttons |= 0x02;

        _writer.WriteSample(new TraceSample
        {
            Tick = tick,
            X = input.X,
            Y = input.Y,
            Dx = dx,
            Dy = dy,
            Buttons = buttons
        });

        _prevX = input.X;
        _prevY = input.Y;
        _hasPrev = true;
    }

    public void Complete()
    {
        _writer.Flush();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _writer.Dispose();
    }
}
