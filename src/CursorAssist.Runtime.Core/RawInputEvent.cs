namespace CursorAssist.Runtime.Core;

/// <summary>
/// Normalized raw input event from the OS adapter.
/// Platform adapters convert OS-specific events into this type.
/// </summary>
public readonly record struct RawInputEvent(
    float Dx,
    float Dy,
    bool PrimaryDown,
    bool SecondaryDown,
    long TimestampTicks);
