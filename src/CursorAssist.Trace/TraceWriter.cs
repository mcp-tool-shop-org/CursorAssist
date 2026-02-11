using System.Text.Json;

namespace CursorAssist.Trace;

/// <summary>
/// Writes .castrace.jsonl files. One JSON object per line.
/// Thread-safe via lock on the underlying writer.
/// </summary>
public sealed class TraceWriter : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly object _lock = new();
    private bool _disposed;

    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TraceWriter(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (dir is not null) Directory.CreateDirectory(dir);
        _writer = new StreamWriter(path, append: false, System.Text.Encoding.UTF8);
    }

    public TraceWriter(Stream stream)
    {
        _writer = new StreamWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
    }

    public void WriteHeader(TraceHeader header)
    {
        lock (_lock)
        {
            _writer.WriteLine(JsonSerializer.Serialize(header, Opts));
        }
    }

    public void WriteSample(in TraceSample sample)
    {
        lock (_lock)
        {
            _writer.WriteLine(JsonSerializer.Serialize(sample, Opts));
        }
    }

    public void Flush()
    {
        lock (_lock) { _writer.Flush(); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_lock) { _writer.Dispose(); }
    }
}
