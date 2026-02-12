# CursorAssist

A deterministic engine for assistive cursor control, accessibility benchmarking, and adaptive motor-skill training.

CursorAssist helps people with motor impairments (tremor, limited range, fatigue) use a mouse more comfortably. It also provides a training environment for building cursor dexterity through the included MouseTrainer game.

## What's Inside

This is a unified workspace containing two product surfaces that share a common engine:

**CursorAssist** -- real-time cursor assistance for accessibility
- Tremor compensation via EMA smoothing and phase correction
- Adaptive deadzones, edge resistance, and target magnetism
- Motor profiling with versioned schemas
- Deterministic policy mapping: same profile always produces the same config

**MouseTrainer** -- deterministic cursor dexterity game
- Fixed 60 Hz simulation with composable blueprint mutators
- Platform-stable run identity via xorshift32 RNG and FNV-1a hashing
- ReflexGates mode with oscillating apertures and seed-based level generation

## NuGet Packages

| Package | Description |
|---------|-------------|
| `CursorAssist.Canon` | Versioned immutable schemas and DTOs for motor profiles, assistive configs, and accessibility reports. Zero dependencies. |
| `CursorAssist.Trace` | JSONL trace format for cursor input recording and playback. Thread-safe writer/reader. Zero dependencies. |
| `CursorAssist.Policy` | Deterministic mapper from motor profiles to assistive configs. DSP-grounded tremor compensation with EMA cutoff formulas. |
| `CursorAssist.Engine` | Input transform pipeline with 60 Hz accumulator, composable IInputTransform chain, and metrics collection. |
| `MouseTrainer.Domain` | Deterministic xorshift32 RNG, FNV-1a hashing, game events, and run identity primitives. |
| `MouseTrainer.Simulation` | Fixed-timestep game loop with blueprint mutators, replay recording, and session management. |
| `MouseTrainer.Audio` | Event-driven audio cue system with deterministic volume/pitch jitter and asset verification. |

## Architecture

```
CursorAssist libraries:
  Canon          --> (nothing)         Schemas + DTOs (leaf)
  Trace          --> (nothing)         Input recording (leaf)
  Policy         --> Canon             Profile-to-config mapping
  Engine         --> Canon, Trace      Transform pipeline
  Runtime.Core   --> Engine, Policy    Thread management, config swap
  Runtime.Windows --> Runtime.Core     Win32 hooks, raw input

MouseTrainer libraries:
  Domain         --> (nothing)         RNG, events, run identity (leaf)
  Simulation     --> Domain            Game loop, mutators, levels
  Audio          --> Domain            Cue system, asset verification

Apps:
  MouseTrainer.MauiHost  --> all MouseTrainer libs    MAUI desktop game
  CursorAssist.Pilot     --> all CursorAssist libs    Tray-based assistant
```

## Building

```bash
# Build all libraries
dotnet build

# Run MouseTrainer tests (214 tests)
dotnet test tests/MouseTrainer.Tests/

# Run CursorAssist tests
dotnet test tests/CursorAssist.Tests/
```

## Design Principles

- **Determinism is constitutional.** Same input produces the same output, always. No `DateTime.Now`, no `Random`, no platform-dependent floats in the hot path.
- **Modular with enforced boundaries.** One-way dependencies, no cycles. Domain and Canon are leaves. Apps are composition roots.
- **Protocol-grade identity.** IDs are permanent and frozen. FNV-1a hashing with canonical parameter serialization.
- **Accessibility is the product.** CursorAssist exists to make computers usable for people with motor impairments. MouseTrainer exists to help people build the dexterity to need less assistance over time.

## License

[MIT](LICENSE)
