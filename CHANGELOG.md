# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [1.0.3] - 2026-02-27

### Added
- Shipcheck compliance: SECURITY.md, CHANGELOG.md, SHIP_GATE.md, SCORECARD.md
- Security & Data Scope section in README

## [1.0.2]

### Added
- NuGet package publishing for Canon, Trace, Policy, Engine
- CursorAssist.Benchmark.Cli replay benchmarking tool
- CursorAssist.Profile.Cli motor profiling tool

## [1.0.1]

### Fixed
- Minor bug fixes and stability improvements

## [1.0.0]

### Added
- Deterministic input transform pipeline with 60Hz accumulator loop
- Five composable transforms: SoftDeadzone, Smoothing, PhaseCompensation, DirectionalIntent, TargetMagnetism
- CursorAssist.Canon — versioned immutable schemas and DTOs
- CursorAssist.Trace — JSONL trace format for cursor input recording
- CursorAssist.Policy — deterministic profile-to-config mapper with DSP-grounded formulas
- CursorAssist.Engine — transform pipeline with FNV-1a hash verification
- MouseTrainer game with fixed-timestep simulation and composable blueprint mutators
- 214+ tests across engine, policy, canon, trace, and game modules
