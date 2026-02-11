using CursorAssist.Canon.Schemas;

namespace CursorAssist.Runtime.Core;

/// <summary>
/// Built-in safe default configs for first-time users and pilot sessions.
/// These represent conservative, well-tested parameter sets that prioritize
/// safety and stability over maximum assistance.
/// </summary>
public static class SafeDefaults
{
    /// <summary>
    /// Minimal assistance: light smoothing only.
    /// Least intrusive — suitable for users who want subtle stabilization.
    /// </summary>
    public static AssistiveConfig Minimal() => new()
    {
        SourceProfileId = "safe-default-minimal",
        SmoothingStrength = 0.3f,
        SmoothingMinAlpha = 0.30f,
        SmoothingMaxAlpha = 0.90f,
        DeadzoneRadiusVpx = 0.5f,
        // Everything else zero/disabled — least intrusive
    };

    /// <summary>
    /// Moderate assistance: smoothing + deadzone + light phase compensation + intent boost.
    /// Good starting point for most users with mild tremor or motor impairment.
    /// </summary>
    public static AssistiveConfig Moderate() => new()
    {
        SourceProfileId = "safe-default-moderate",
        SmoothingStrength = 0.5f,
        SmoothingMinAlpha = 0.25f,
        SmoothingMaxAlpha = 0.90f,
        DeadzoneRadiusVpx = 1.0f,
        PhaseCompensationGainS = 0.007f,
        IntentBoostStrength = 0.3f,
        IntentCoherenceThreshold = 0.85f,
    };
}
