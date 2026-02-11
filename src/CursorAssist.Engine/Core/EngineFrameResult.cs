namespace CursorAssist.Engine.Core;

/// <summary>
/// Output of a single engine step. Contains the final cursor state,
/// events emitted, metrics summary, and determinism hash for the tick.
/// </summary>
public sealed class EngineFrameResult
{
    public required long Tick { get; init; }

    /// <summary>Final cursor position after all transforms.</summary>
    public required InputSample FinalCursor { get; init; }

    /// <summary>Raw input before transforms (for delta comparison).</summary>
    public required InputSample RawInput { get; init; }

    /// <summary>Events emitted during this tick.</summary>
    public required IReadOnlyList<EngineEvent> Events { get; init; }

    /// <summary>Rendering interpolation alpha [0, 1).</summary>
    public required float Alpha { get; init; }

    /// <summary>Running FNV-1a hash for determinism verification.</summary>
    public required ulong DeterminismHash { get; init; }
}
