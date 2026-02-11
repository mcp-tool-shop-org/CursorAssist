namespace CursorAssist.Engine.Core;

/// <summary>
/// Event emitted by the engine pipeline (target hit, miss, session markers).
/// Analogous to MouseTrainer's GameEvent but generalized for all three expressions.
/// </summary>
public readonly record struct EngineEvent(
    EngineEventType Type,
    float Intensity = 1f,
    int Arg0 = 0,
    int Arg1 = 0,
    string? Tag = null);

public enum EngineEventType
{
    None = 0,
    Tick,
    TargetEnter,
    TargetExit,
    TargetHit,
    TargetMiss,
    SessionStart,
    SessionEnd,
    MagnetismEngaged,
    MagnetismDisengaged
}
