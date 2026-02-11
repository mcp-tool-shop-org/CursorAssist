namespace CursorAssist.Runtime.Core;

/// <summary>
/// Output delta produced by the engine thread for the injection thread.
/// </summary>
public readonly record struct AssistedDelta(
    float Dx,
    float Dy,
    long Tick);
