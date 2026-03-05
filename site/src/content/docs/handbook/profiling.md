---
title: Motor Profiling
description: DSP-grounded profile-to-config mapping.
sidebar:
  order: 3
---

CursorAssist uses motor profiles to adapt its behavior to individual users. The Policy package maps profiles to assistive configurations using DSP-grounded formulas.

## Motor profile inputs

A motor profile captures measurable characteristics of a user's cursor control:

| Field | Unit | Meaning |
|-------|------|---------|
| TremorFrequencyHz | Hz | Dominant tremor frequency |
| TremorAmplitudeVpx | virtual px | Tremor amplitude |
| PathEfficiency | ratio (0-1) | How directly the cursor reaches targets |
| OvershootRate | rate | How often the cursor overshoots targets |

## Profile-to-config mapping

```csharp
var profile = new MotorProfile
{
    TremorFrequencyHz = 6f,
    TremorAmplitudeVpx = 4.5f,
    PathEfficiency = 0.72f,
    OvershootRate = 1.2f,
};

AssistiveConfig config = ProfileToConfigMapper.Map(profile);
```

The mapper derives configuration values using closed-form DSP formulas:

| Output | Formula basis | Example (6 Hz tremor) |
|--------|--------------|----------------------|
| SmoothingMinAlpha | Closed-form EMA cutoff from tremor frequency | ~0.31 |
| DeadzoneRadiusVpx | Power-law frequency-weighted | ~2.7 |
| MagnetismRadiusVpx | Scaled from path deficiency | ~63.6 |
| PhaseCompensationGainS | Conservative lag offset | ~0.005 |

## DSP grounding

The smoothing filter is not ad hoc — the EMA cutoff frequency is derived from the formula `fc = alpha * Fs / (2 * pi)`, where `Fs` is the sampling rate (60 Hz). Deadzone radii use power-law frequency weighting, and phase compensation is velocity-attenuated to prevent overshoot at high speeds.
