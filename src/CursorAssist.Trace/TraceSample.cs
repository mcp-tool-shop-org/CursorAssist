using System.Text.Json.Serialization;

namespace CursorAssist.Trace;

/// <summary>
/// Per-tick input sample in a .castrace.jsonl file.
/// One record per fixed-timestep tick.
/// </summary>
public readonly record struct TraceSample()
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "tick";

    [JsonPropertyName("tick")]
    public required int Tick { get; init; }

    [JsonPropertyName("x")]
    public required float X { get; init; }

    [JsonPropertyName("y")]
    public required float Y { get; init; }

    [JsonPropertyName("dx")]
    public float Dx { get; init; }

    [JsonPropertyName("dy")]
    public float Dy { get; init; }

    /// <summary>Button bitmask: bit 0 = primary, bit 1 = secondary.</summary>
    [JsonPropertyName("btn")]
    public byte Buttons { get; init; }
}
