namespace CursorAssist.Runtime.Core;

/// <summary>
/// Hard safety limits enforced at runtime, independent of config validation.
/// These are the last line of defense against pathological output.
/// CanonValidator catches invalid configs at creation time; these clamps
/// catch anything that slips through (e.g., deserialization, direct construction).
/// </summary>
public static class RuntimeLimits
{
    /// <summary>Maximum assisted delta per tick in pixels. 50px at 60Hz = 3000 px/s.</summary>
    public const float MaxDeltaPerTick = 50f;

    /// <summary>Minimum runtime-enforced smoothing alpha. Below 0.05 → fc &lt; 0.5 Hz (unusable).</summary>
    public const float MinAlpha = 0.05f;

    /// <summary>Maximum runtime-enforced smoothing alpha. Above 0.98 → near-zero filtering.</summary>
    public const float MaxAlpha = 0.98f;

    /// <summary>Maximum deadzone radius enforced at runtime (vpx).</summary>
    public const float MaxDeadzoneRadius = 3.0f;

    /// <summary>Maximum phase compensation gain in seconds. Above 100ms risks overshoot.</summary>
    public const float MaxPhaseCompGainS = 0.1f;
}
