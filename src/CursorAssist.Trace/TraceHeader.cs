using System.Text.Json.Serialization;

namespace CursorAssist.Trace;

/// <summary>
/// First record in a .castrace.jsonl file. Describes the source session.
/// </summary>
public sealed record TraceHeader
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "header";

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 1;

    [JsonPropertyName("sourceApp")]
    public required string SourceApp { get; init; }

    [JsonPropertyName("sourceVersion")]
    public string? SourceVersion { get; init; }

    [JsonPropertyName("fixedHz")]
    public int FixedHz { get; init; } = 60;

    [JsonPropertyName("runSeed")]
    public uint? RunSeed { get; init; }

    [JsonPropertyName("runId")]
    public string? RunId { get; init; }

    /// <summary>DPI at capture time, if known. Null = unknown.</summary>
    [JsonPropertyName("dpi")]
    public float? Dpi { get; init; }

    /// <summary>Virtual coordinate space width.</summary>
    [JsonPropertyName("virtualWidth")]
    public float VirtualWidth { get; init; } = 1920f;

    /// <summary>Virtual coordinate space height.</summary>
    [JsonPropertyName("virtualHeight")]
    public float VirtualHeight { get; init; } = 1080f;

    [JsonPropertyName("createdUtc")]
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
}
