using System.Text.Json;

namespace CursorAssist.Trace;

/// <summary>
/// Reads .castrace.jsonl files. Parses header then streams tick samples.
/// </summary>
public sealed class TraceReader : IDisposable
{
    private readonly StreamReader _reader;

    public TraceHeader? Header { get; private set; }

    public TraceReader(string path)
    {
        _reader = new StreamReader(path, System.Text.Encoding.UTF8);
    }

    public TraceReader(Stream stream)
    {
        _reader = new StreamReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
    }

    /// <summary>
    /// Read the header record. Must be called before ReadSamples.
    /// </summary>
    public TraceHeader ReadHeader()
    {
        var line = _reader.ReadLine()
            ?? throw new InvalidDataException("Empty trace file.");

        var header = JsonSerializer.Deserialize<TraceHeader>(line, Opts)
            ?? throw new InvalidDataException("Invalid trace header.");

        if (header.SchemaVersion < 1)
            throw new InvalidDataException($"Unsupported schema version: {header.SchemaVersion}");

        Header = header;
        return header;
    }

    /// <summary>
    /// Enumerate all tick samples after the header.
    /// </summary>
    public IEnumerable<TraceSample> ReadSamples()
    {
        while (_reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Check type field before full deserialize
            if (!line.Contains("\"tick\"")) continue;

            var sample = JsonSerializer.Deserialize<TraceSample>(line, Opts);
            yield return sample;
        }
    }

    public void Dispose() => _reader.Dispose();

    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
