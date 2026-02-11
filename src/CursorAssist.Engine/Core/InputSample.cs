namespace CursorAssist.Engine.Core;

/// <summary>
/// Raw input sample captured each tick. Position is in virtual pixel space.
/// This is the CursorAssist equivalent of MouseTrainer's PointerInput,
/// generalized for the assist/benchmark/training pipeline.
/// </summary>
public readonly record struct InputSample(
    float X,
    float Y,
    float Dx,
    float Dy,
    bool PrimaryDown,
    bool SecondaryDown,
    long Tick);
