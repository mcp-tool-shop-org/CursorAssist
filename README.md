<p align="center"><img src="logo.png" alt="logo" width="200"></p>

# CursorAssist

> Part of [MCP Tool Shop](https://mcptoolshop.com)

[![NuGet Publish](https://github.com/mcp-tool-shop-org/CursorAssist/actions/workflows/publish.yml/badge.svg)](https://github.com/mcp-tool-shop-org/CursorAssist/actions/workflows/publish.yml)
![.NET 8](https://img.shields.io/badge/.NET-8-512BD4)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**A deterministic engine for assistive cursor control, UI accessibility benchmarking, and adaptive motor-skill training.**

---

## Why CursorAssist?

- **Tremor compensation that actually works.** DSP-grounded EMA smoothing with velocity-adaptive alpha, frequency-aware cutoff, optional dual-pole (-40 dB/decade), and phase compensation -- not a toy low-pass filter.
- **Deterministic by design.** Same input always produces the same output. No `DateTime.Now`, no `Random`, no platform-dependent floats. Every frame is reproducible and verifiable via FNV-1a hash chains.
- **Modular NuGet packages.** Use just the schemas (Canon), just the trace format (Trace), just the policy mapper (Policy), or the full transform pipeline (Engine). No forced coupling.
- **Motor profiling with real DSP math.** Power-law frequency-weighted deadzones, closed-form EMA cutoff from tremor frequency, directional intent detection via cosine coherence, and hysteresis on every engagement boundary.
- **214+ tests.** Every transform, every policy rule, every edge case.

---

## Two Product Surfaces, One Engine

This workspace contains two products that share a common deterministic core:

### CursorAssist -- Real-Time Cursor Assistance

For people with motor impairments (tremor, limited range, fatigue). Runs as a system tray application that intercepts and transforms raw pointer input in real time.

- Tremor compensation via velocity-adaptive EMA smoothing and phase correction
- Adaptive soft deadzones with quadratic compression (no hard edges)
- Edge resistance and target magnetism with hysteresis
- Motor profiling with versioned schemas
- Deterministic policy mapping: same profile always produces the same config

### MouseTrainer -- Deterministic Cursor Dexterity Game

For building the dexterity to need less assistance over time. A .NET MAUI desktop game with fixed-timestep simulation.

- Fixed 60 Hz simulation with composable blueprint mutators
- Platform-stable run identity via xorshift32 RNG and FNV-1a hashing
- Drag-and-Drop Gauntlet mode for real-world cursor task training
- Event-driven audio cue system with deterministic volume/pitch jitter

---

## NuGet Packages

The four CursorAssist libraries are published to NuGet as independent packages:

| Package | NuGet | Description |
|---------|-------|-------------|
| **CursorAssist.Canon** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Canon)](https://www.nuget.org/packages/CursorAssist.Canon) | Versioned immutable schemas and DTOs for motor profiles, assistive configs, difficulty plans, and accessibility reports. Zero dependencies. |
| **CursorAssist.Trace** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Trace)](https://www.nuget.org/packages/CursorAssist.Trace) | JSONL trace format (`.castrace.jsonl`) for cursor input recording and playback. Thread-safe writer/reader. Zero dependencies. |
| **CursorAssist.Policy** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Policy)](https://www.nuget.org/packages/CursorAssist.Policy) | Deterministic mapper from motor profiles to assistive configs. DSP-grounded tremor compensation with EMA cutoff formulas, power-law deadzones, and phase compensation. Depends on Canon. |
| **CursorAssist.Engine** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Engine)](https://www.nuget.org/packages/CursorAssist.Engine) | Input transform pipeline with 60 Hz accumulator, composable `IInputTransform` chain, and metrics collection. Depends on Canon and Trace. |

### Install

```bash
# Install individual packages
dotnet add package CursorAssist.Canon
dotnet add package CursorAssist.Trace
dotnet add package CursorAssist.Policy
dotnet add package CursorAssist.Engine
```

Or add to your `.csproj`:

```xml
<PackageReference Include="CursorAssist.Canon" Version="1.0.0" />
<PackageReference Include="CursorAssist.Trace" Version="1.0.0" />
<PackageReference Include="CursorAssist.Policy" Version="1.0.0" />
<PackageReference Include="CursorAssist.Engine" Version="1.0.0" />
```

> You only need the packages you use. Canon and Trace are zero-dependency leaves. Policy depends on Canon. Engine depends on Canon and Trace.

---

## Quick Start

### Map a motor profile to an assistive config (Policy)

```csharp
using CursorAssist.Canon.Schemas;
using CursorAssist.Policy;

var profile = new MotorProfile
{
    ProfileId = "user-001",
    CreatedUtc = DateTimeOffset.UtcNow,
    TremorFrequencyHz = 6f,
    TremorAmplitudeVpx = 4.5f,
    PathEfficiency = 0.72f,
    OvershootRate = 1.2f,
};

AssistiveConfig config = ProfileToConfigMapper.Map(profile);

// config.SmoothingMinAlpha      --> ~0.31 (closed-form from 6 Hz tremor)
// config.DeadzoneRadiusVpx      --> ~2.7  (power-law freq-weighted)
// config.MagnetismRadiusVpx     --> ~63.6 (scaled from path deficiency)
// config.PhaseCompensationGainS --> ~0.005 (conservative lag offset)
```

### Build and run the transform pipeline (Engine)

```csharp
using CursorAssist.Engine.Core;
using CursorAssist.Engine.Transforms;

var pipeline = new TransformPipeline()
    .Add(new SoftDeadzoneTransform())
    .Add(new SmoothingTransform())
    .Add(new PhaseCompensationTransform())
    .Add(new DirectionalIntentTransform())
    .Add(new TargetMagnetismTransform());

var engine = new DeterministicPipeline(pipeline, fixedHz: 60);

var context = new TransformContext
{
    Tick = 0,
    Dt = 1f / 60f,
    Config = config,
    Targets = []
};

var raw = new InputSample(X: 500f, Y: 300f, Dx: 2.1f, Dy: -0.8f,
                          PrimaryDown: false, SecondaryDown: false, Tick: 0);

EngineFrameResult result = engine.FixedStep(in raw, context);
// result.FinalCursor     --> smoothed, deadzone-filtered, phase-compensated position
// result.DeterminismHash --> FNV-1a hash for replay verification
```

### Record a trace (Trace)

```csharp
using CursorAssist.Trace;

using var writer = new TraceWriter("session.castrace.jsonl");
writer.WriteHeader(new TraceHeader
{
    SessionId = "sess-001",
    StartedUtc = DateTimeOffset.UtcNow,
    TickRateHz = 60
});

writer.WriteSample(new TraceSample { Tick = 0, X = 500f, Y = 300f });
writer.WriteSample(new TraceSample { Tick = 1, X = 502.1f, Y = 299.2f });
```

---

## Architecture

```
CursorAssist libraries:
  Canon            --> (nothing)         Schemas + DTOs (leaf)
  Trace            --> (nothing)         Input recording (leaf)
  Policy           --> Canon             Profile-to-config mapping
  Engine           --> Canon, Trace      Transform pipeline

  Runtime.Core     --> Engine, Policy    Thread management, config swap
  Runtime.Windows  --> Runtime.Core      Win32 hooks, raw input

MouseTrainer libraries:
  Domain           --> (nothing)         RNG, events, run identity (leaf)
  Simulation       --> Domain            Game loop, mutators, levels
  Audio            --> Domain            Cue system, asset verification

Apps:
  CursorAssist.Pilot       --> all CursorAssist libs    Tray-based assistant
  MouseTrainer.MauiHost    --> all MouseTrainer libs     MAUI desktop game

CLI tools:
  CursorAssist.Benchmark.Cli   --> Engine, Policy       Replay benchmarking
  CursorAssist.Profile.Cli     --> Engine, Canon         Motor profiling
```

### Transform Pipeline Order

```
Raw Input --> SoftDeadzone --> Smoothing --> PhaseCompensation --> DirectionalIntent --> TargetMagnetism --> Output
```

Each transform implements `IInputTransform` and is composed via `TransformPipeline`. The `DeterministicPipeline` wraps the chain in a fixed-timestep accumulator loop with FNV-1a hash verification on every tick.

---

## Design Principles

- **Determinism is constitutional.** Same input produces the same output, always. No `DateTime.Now`, no `Random`, no platform-dependent floats in the hot path. Every frame is hash-verified.
- **DSP-grounded, not ad hoc.** EMA cutoff frequencies are derived from closed-form formulas (`fc = alpha * Fs / 2pi`). Deadzone radii use power-law frequency weighting. Phase compensation is velocity-attenuated to prevent overshoot.
- **Modular with enforced boundaries.** One-way dependencies, no cycles. Canon and Trace are leaves. Apps are composition roots.
- **Protocol-grade identity.** IDs are permanent and frozen. FNV-1a hashing with canonical parameter serialization. xorshift32 RNG for reproducible game sessions.
- **Accessibility is the product.** CursorAssist exists to make computers usable for people with motor impairments. MouseTrainer exists to help people build the dexterity to need less assistance over time.

---

## Building from Source

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Windows 10/11 (for Runtime.Windows and MouseTrainer.MauiHost)
- Any OS for the core libraries (Canon, Trace, Policy, Engine are platform-agnostic)

### Build

```bash
# Clone
git clone https://github.com/mcp-tool-shop-org/CursorAssist.git
cd CursorAssist

# Build all projects
dotnet build

# Run all tests (214+ tests)
dotnet test

# Run CursorAssist tests only
dotnet test tests/CursorAssist.Tests/

# Run MouseTrainer tests only
dotnet test tests/MouseTrainer.Tests/
```

### Pack NuGet packages locally

```bash
dotnet pack src/CursorAssist.Canon/CursorAssist.Canon.csproj -c Release -o ./nupkg
dotnet pack src/CursorAssist.Trace/CursorAssist.Trace.csproj -c Release -o ./nupkg
dotnet pack src/CursorAssist.Policy/CursorAssist.Policy.csproj -c Release -o ./nupkg
dotnet pack src/CursorAssist.Engine/CursorAssist.Engine.csproj -c Release -o ./nupkg
```

---

## Project Structure

```
CursorAssist/
├── src/
│   ├── CursorAssist.Canon/            # Schemas + DTOs (NuGet leaf)
│   ├── CursorAssist.Trace/            # JSONL trace format (NuGet leaf)
│   ├── CursorAssist.Policy/           # Profile --> config mapper (NuGet)
│   ├── CursorAssist.Engine/           # Transform pipeline (NuGet)
│   │   ├── Core/                      # Pipeline, InputSample, TransformContext
│   │   ├── Transforms/                # SoftDeadzone, Smoothing, PhaseComp, Intent, Magnetism
│   │   ├── Analysis/                  # TremorAnalyzer, CalibrationSession
│   │   ├── Layout/                    # UILayout for target/button mapping
│   │   ├── Mapping/                   # Engine-internal mapper (delegates to Policy)
│   │   └── Metrics/                   # IMetricsSink, Benchmark, Tracing, MotorProfile sinks
│   ├── CursorAssist.Runtime.Core/     # Thread management, config hot-swap
│   ├── CursorAssist.Runtime.Windows/  # Win32 hooks, raw input capture
│   ├── CursorAssist.Pilot/            # Tray-based assistant app
│   ├── CursorAssist.Benchmark.Cli/    # Replay benchmarking tool
│   ├── CursorAssist.Profile.Cli/      # Motor profiling tool
│   ├── MouseTrainer.Domain/           # RNG, events, run identity
│   ├── MouseTrainer.Simulation/       # Game loop, mutators, levels
│   ├── MouseTrainer.Audio/            # Event-driven audio cues
│   └── MouseTrainer.MauiHost/         # MAUI desktop game app
├── tests/
│   ├── CursorAssist.Tests/            # Engine, Policy, Canon, Trace tests
│   └── MouseTrainer.Tests/            # Domain, Simulation, Audio tests
├── tools/
│   └── MouseTrainer.AudioGen/         # Audio asset generator
├── docs/
│   ├── product-boundary.md            # MouseTrainer scope definition
│   └── modular.manifesto.md           # Modularity principles
└── MouseTrainer.Deterministic.sln     # Solution file
```

---

## License

[MIT](LICENSE)
