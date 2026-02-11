namespace CursorAssist.Canon.Validation;

/// <summary>
/// Validates canon DTOs by schema version + required fields.
/// Keeps validation simple and version-aware — no runtime reflection.
/// </summary>
public static class CanonValidator
{
    public static ValidationResult Validate(Schemas.MotorProfile profile)
    {
        var errors = new List<string>();

        if (profile.Version != Schemas.MotorProfile.SchemaVersion)
            errors.Add($"Unsupported MotorProfile version {profile.Version}; expected {Schemas.MotorProfile.SchemaVersion}.");

        if (string.IsNullOrWhiteSpace(profile.ProfileId))
            errors.Add("ProfileId is required.");

        if (profile.CreatedUtc == default)
            errors.Add("CreatedUtc is required.");

        if (profile.PathEfficiency is < 0f or > 1f)
            errors.Add($"PathEfficiency must be [0, 1]; got {profile.PathEfficiency}.");

        if (profile.TremorFrequencyHz < 0f)
            errors.Add($"TremorFrequencyHz must be >= 0; got {profile.TremorFrequencyHz}.");

        if (profile.TremorAmplitudeVpx < 0f)
            errors.Add($"TremorAmplitudeVpx must be >= 0; got {profile.TremorAmplitudeVpx}.");

        if (profile.SampleCount < 0)
            errors.Add($"SampleCount must be >= 0; got {profile.SampleCount}.");

        return new ValidationResult(errors);
    }

    public static ValidationResult Validate(Schemas.AssistiveConfig config)
    {
        var errors = new List<string>();

        // Accept schema versions 1 and 2 for backward compatibility.
        // v1 configs lack IntentDisengageThreshold (defaults to 0.65).
        if (config.Version is < 1 or > Schemas.AssistiveConfig.SchemaVersion)
            errors.Add($"Unsupported AssistiveConfig version {config.Version}; expected 1–{Schemas.AssistiveConfig.SchemaVersion}.");

        if (string.IsNullOrWhiteSpace(config.SourceProfileId))
            errors.Add("SourceProfileId is required.");

        if (config.SmoothingStrength is < 0f or > 1f)
            errors.Add($"SmoothingStrength must be [0, 1]; got {config.SmoothingStrength}.");

        if (config.SmoothingMinAlpha is < 0.05f or > 1f)
            errors.Add($"SmoothingMinAlpha must be [0.05, 1]; got {config.SmoothingMinAlpha}. Below 0.05 → fc < 0.5 Hz (unusable).");

        if (config.SmoothingMaxAlpha is < 0.05f or > 1f)
            errors.Add($"SmoothingMaxAlpha must be [0.05, 1]; got {config.SmoothingMaxAlpha}.");

        if (config.SmoothingMinAlpha > config.SmoothingMaxAlpha)
            errors.Add($"SmoothingMinAlpha ({config.SmoothingMinAlpha}) must be <= SmoothingMaxAlpha ({config.SmoothingMaxAlpha}).");

        if (config.SmoothingVelocityLow < 0f)
            errors.Add($"SmoothingVelocityLow must be >= 0; got {config.SmoothingVelocityLow}.");

        if (config.SmoothingVelocityHigh <= 0f)
            errors.Add($"SmoothingVelocityHigh must be > 0; got {config.SmoothingVelocityHigh}.");

        if (config.SmoothingVelocityLow >= config.SmoothingVelocityHigh)
            errors.Add($"SmoothingVelocityLow ({config.SmoothingVelocityLow}) must be < SmoothingVelocityHigh ({config.SmoothingVelocityHigh}).");

        if (config.MagnetismStrength is < 0f or > 1f)
            errors.Add($"MagnetismStrength must be [0, 1]; got {config.MagnetismStrength}.");

        if (config.MagnetismRadiusVpx < 0f)
            errors.Add($"MagnetismRadiusVpx must be >= 0; got {config.MagnetismRadiusVpx}.");

        if (config.EdgeResistance is < 0f or > 1f)
            errors.Add($"EdgeResistance must be [0, 1]; got {config.EdgeResistance}.");

        if (config.DeadzoneRadiusVpx < 0f)
            errors.Add($"DeadzoneRadiusVpx must be >= 0; got {config.DeadzoneRadiusVpx}.");

        if (config.PhaseCompensationGainS is < 0f or > 0.1f)
            errors.Add($"PhaseCompensationGainS must be [0, 0.1]; got {config.PhaseCompensationGainS}. Above 100ms compensation risks overshoot.");

        if (config.IntentBoostStrength is < 0f or > 1f)
            errors.Add($"IntentBoostStrength must be [0, 1]; got {config.IntentBoostStrength}.");

        if (config.IntentCoherenceThreshold is < 0.5f or > 1f)
            errors.Add($"IntentCoherenceThreshold must be [0.5, 1]; got {config.IntentCoherenceThreshold}.");

        if (config.IntentDisengageThreshold is < 0.3f or > 1f)
            errors.Add($"IntentDisengageThreshold must be [0.3, 1]; got {config.IntentDisengageThreshold}.");

        if (config.IntentDisengageThreshold > config.IntentCoherenceThreshold)
            errors.Add($"IntentDisengageThreshold ({config.IntentDisengageThreshold}) must be <= IntentCoherenceThreshold ({config.IntentCoherenceThreshold}).");

        return new ValidationResult(errors);
    }

    public static ValidationResult Validate(Schemas.UIAccessibilityReport report)
    {
        var errors = new List<string>();

        if (report.Version != Schemas.UIAccessibilityReport.SchemaVersion)
            errors.Add($"Unsupported UIAccessibilityReport version {report.Version}; expected {Schemas.UIAccessibilityReport.SchemaVersion}.");

        if (string.IsNullOrWhiteSpace(report.LayoutId))
            errors.Add("LayoutId is required.");

        if (report.GeneratedUtc == default)
            errors.Add("GeneratedUtc is required.");

        if (report.TrialCount < 0)
            errors.Add($"TrialCount must be >= 0; got {report.TrialCount}.");

        if (report.ErrorRate is < 0f or > 1f)
            errors.Add($"ErrorRate must be [0, 1]; got {report.ErrorRate}.");

        return new ValidationResult(errors);
    }

    public static ValidationResult Validate(Schemas.AdaptiveDifficultyPlan plan)
    {
        var errors = new List<string>();

        if (plan.Version != Schemas.AdaptiveDifficultyPlan.SchemaVersion)
            errors.Add($"Unsupported AdaptiveDifficultyPlan version {plan.Version}; expected {Schemas.AdaptiveDifficultyPlan.SchemaVersion}.");

        if (string.IsNullOrWhiteSpace(plan.SourceProfileId))
            errors.Add("SourceProfileId is required.");

        if (plan.GeneratedUtc == default)
            errors.Add("GeneratedUtc is required.");

        if (plan.TargetSizeMultiplier is < 0.25f or > 4f)
            errors.Add($"TargetSizeMultiplier must be [0.25, 4.0]; got {plan.TargetSizeMultiplier}.");

        if (plan.TargetSpeedMultiplier is < 0.25f or > 4f)
            errors.Add($"TargetSpeedMultiplier must be [0.25, 4.0]; got {plan.TargetSpeedMultiplier}.");

        return new ValidationResult(errors);
    }
}

public readonly record struct ValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}
