---
title: Reference
description: NuGet packages, project structure, and security scope.
sidebar:
  order: 5
---

## NuGet packages

| Package | Depends on | Description |
|---------|-----------|-------------|
| CursorAssist.Canon | (nothing) | Versioned immutable schemas and DTOs for motor profiles, assistive configs, difficulty plans |
| CursorAssist.Trace | (nothing) | JSONL trace format (`.castrace.jsonl`) for cursor input recording and playback |
| CursorAssist.Policy | Canon | Deterministic mapper from motor profiles to assistive configs |
| CursorAssist.Engine | Canon, Trace | Input transform pipeline with 60 Hz accumulator and hash verification |

## CLI tools

| Tool | Purpose |
|------|---------|
| CursorAssist.Benchmark.Cli | Replay benchmarking |
| CursorAssist.Profile.Cli | Motor profiling |

## Test coverage

214+ tests across both products:

| Category | What it covers |
|----------|---------------|
| Architecture | Dependency boundary enforcement |
| Determinism | Replay regression, RNG, session controller |
| Levels | Generator extraction |
| Mutators | Blueprint mutator correctness and composition |
| Persistence | Session store |
| Replay | Serializer, recorder, verifier, quantization |
| Runs | RunDescriptor golden hashes and identity |
| Scoring | Score breakdown |
| Utility | LEB128 encoding |

## Security and data scope

| Aspect | Detail |
|--------|--------|
| Data accessed | Raw pointer input coordinates, motor profile JSON, trace recordings, MAUI local storage |
| Data NOT accessed | No cloud sync, no telemetry, no analytics, no network calls |
| Permissions | Raw pointer input (Windows hooks), file system for profiles and traces |

See [SECURITY.md](https://github.com/mcp-tool-shop-org/CursorAssist/blob/main/SECURITY.md) for vulnerability reporting.
