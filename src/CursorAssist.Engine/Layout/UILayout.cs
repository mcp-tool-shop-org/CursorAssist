using System.Text.Json.Serialization;
using CursorAssist.Engine.Core;

namespace CursorAssist.Engine.Layout;

/// <summary>
/// A UI layout for benchmarking. Collection of rectangular targets
/// with optional grouping. Loaded from JSON.
/// </summary>
public sealed record UILayout
{
    [JsonPropertyName("layoutId")]
    public required string LayoutId { get; init; }

    [JsonPropertyName("width")]
    public float Width { get; init; } = 1920f;

    [JsonPropertyName("height")]
    public float Height { get; init; } = 1080f;

    [JsonPropertyName("targets")]
    public required IReadOnlyList<LayoutTarget> Targets { get; init; }
}

public sealed record LayoutTarget
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("centerX")]
    public required float CenterX { get; init; }

    [JsonPropertyName("centerY")]
    public required float CenterY { get; init; }

    [JsonPropertyName("width")]
    public required float Width { get; init; }

    [JsonPropertyName("height")]
    public required float Height { get; init; }

    [JsonPropertyName("group")]
    public string? Group { get; init; }

    public TargetInfo ToTargetInfo() => new(Id, CenterX, CenterY, Width, Height);
}
