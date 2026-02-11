using CursorAssist.Canon.Schemas;

namespace CursorAssist.Engine.Core;

/// <summary>
/// Immutable context provided to each transform per tick.
/// Contains everything a transform needs without hidden state.
/// </summary>
public sealed class TransformContext
{
    public required long Tick { get; init; }
    public required float Dt { get; init; }

    /// <summary>Active targets (may be empty for pure smoothing transforms).</summary>
    public IReadOnlyList<TargetInfo> Targets { get; init; } = [];

    /// <summary>Assistive config driving transform parameters. Null = no assist.</summary>
    public AssistiveConfig? Config { get; init; }

    /// <summary>Motor profile for context-aware transforms. Null = unknown user.</summary>
    public MotorProfile? Profile { get; init; }
}
