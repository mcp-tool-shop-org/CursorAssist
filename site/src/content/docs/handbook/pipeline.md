---
title: Transform Pipeline
description: How the input processing chain works.
sidebar:
  order: 2
---

The CursorAssist Engine processes raw pointer input through a composable chain of transforms, each implementing `IInputTransform`.

## Pipeline order

```
Raw Input → SoftDeadzone → Smoothing → PhaseCompensation → DirectionalIntent → TargetMagnetism → Output
```

Each transform in the chain receives the output of the previous one and applies a specific correction:

| Transform | Purpose |
|-----------|---------|
| SoftDeadzone | Quadratic compression near the center — no hard edges |
| Smoothing | Velocity-adaptive EMA with closed-form cutoff from tremor frequency |
| PhaseCompensation | Lag correction that's velocity-attenuated to prevent overshoot |
| DirectionalIntent | Cosine coherence detection for intended movement direction |
| TargetMagnetism | Cursor attraction toward nearby UI targets with hysteresis |

## Deterministic pipeline

The `DeterministicPipeline` wraps the transform chain in a fixed-timestep accumulator loop running at 60 Hz. Every tick produces an FNV-1a hash for replay verification.

```csharp
var pipeline = new TransformPipeline()
    .Add(new SoftDeadzoneTransform())
    .Add(new SmoothingTransform())
    .Add(new PhaseCompensationTransform())
    .Add(new DirectionalIntentTransform())
    .Add(new TargetMagnetismTransform());

var engine = new DeterministicPipeline(pipeline, fixedHz: 60);

EngineFrameResult result = engine.FixedStep(in raw, context);
// result.FinalCursor     → smoothed, filtered, compensated position
// result.DeterminismHash → FNV-1a hash for replay verification
```

## Composability

Transforms are fully composable. You can add, remove, or reorder them. The pipeline applies them in sequence, passing each transform's output as the next transform's input.
