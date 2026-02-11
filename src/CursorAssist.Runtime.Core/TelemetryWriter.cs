using System.Globalization;
using System.Text.Json;
using CursorAssist.Canon.Schemas;
using CursorAssist.Trace;

namespace CursorAssist.Runtime.Core;

/// <summary>
/// Manages session telemetry output: config snapshot, session summary,
/// and optional tick-level trace files within a timestamped session directory.
///
/// Output structure:
///   {sessionDir}/
///     config.json           (AssistiveConfig snapshot)
///     summary.json          (SessionSummary — written at End())
///     trace.castrace.jsonl  (opt-in tick-level, only if AttachTraceWriter called)
///
/// Thread-safe. Fail-safe: catches I/O exceptions and logs to stderr
/// rather than crashing the host process.
/// </summary>
public sealed class TelemetryWriter : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private string? _sessionDir;
    private string _sessionId = string.Empty;
    private TraceWriter? _traceWriter;
    private bool _disposed;

    /// <summary>Full path to the session directory created by Begin().</summary>
    public string? SessionDir => _sessionDir;

    /// <summary>Session ID assigned by Begin().</summary>
    public string SessionId => _sessionId;

    /// <summary>
    /// Begins a new telemetry session. Creates the session directory
    /// and writes the AssistiveConfig snapshot.
    /// </summary>
    /// <param name="parentDir">Parent directory for session folders (e.g., "./sessions").</param>
    /// <param name="config">Active AssistiveConfig to snapshot.</param>
    /// <returns>The session ID (GUID-based, filesystem-safe).</returns>
    public string Begin(string parentDir, AssistiveConfig config)
    {
        _sessionId = Guid.NewGuid().ToString("N")[..8];
        string timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH-mm-ssZ", CultureInfo.InvariantCulture);
        string dirName = $"{timestamp}_{_sessionId}";
        _sessionDir = Path.Combine(parentDir, dirName);

        try
        {
            Directory.CreateDirectory(_sessionDir);
            string configJson = JsonSerializer.Serialize(config, JsonOpts);
            File.WriteAllText(Path.Combine(_sessionDir, "config.json"), configJson);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[TelemetryWriter] Failed to create session directory: {ex.Message}");
        }

        return _sessionId;
    }

    /// <summary>
    /// Writes the session summary to summary.json in the session directory.
    /// </summary>
    public void End(SessionSummary summary)
    {
        if (_sessionDir is null) return;

        try
        {
            string json = JsonSerializer.Serialize(summary, JsonOpts);
            File.WriteAllText(Path.Combine(_sessionDir, "summary.json"), json);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[TelemetryWriter] Failed to write session summary: {ex.Message}");
        }

        _traceWriter?.Flush();
    }

    /// <summary>
    /// Creates and returns a TraceWriter for tick-level logging.
    /// The trace file is placed in the session directory as trace.castrace.jsonl.
    /// Call after Begin(). Only one trace writer per session.
    /// </summary>
    /// <param name="header">Trace header to write as the first line.</param>
    /// <returns>A TraceWriter for per-tick recording, or null if session not started.</returns>
    public TraceWriter? AttachTraceWriter(TraceHeader header)
    {
        if (_sessionDir is null) return null;

        try
        {
            string tracePath = Path.Combine(_sessionDir, "trace.castrace.jsonl");
            _traceWriter = new TraceWriter(tracePath);
            _traceWriter.WriteHeader(header);
            return _traceWriter;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[TelemetryWriter] Failed to create trace writer: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Computes FNV-1a hash of the serialized AssistiveConfig JSON.
    /// Used as a deduplication key in SessionSummary.ConfigHash.
    /// </summary>
    public static string ComputeConfigHash(AssistiveConfig config)
    {
        string json = JsonSerializer.Serialize(config, JsonOpts);
        ulong hash = 14695981039346656037UL; // FNV offset basis
        foreach (char c in json)
        {
            hash ^= (byte)c;
            hash *= 1099511628211UL; // FNV prime
        }
        return hash.ToString("x16", CultureInfo.InvariantCulture);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _traceWriter?.Dispose();
    }
}
