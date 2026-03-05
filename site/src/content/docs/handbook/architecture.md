---
title: Architecture
description: Module boundaries and design principles.
sidebar:
  order: 4
---

CursorAssist is a modular monolith with strictly enforced one-way dependencies.

## Module dependency graph

```
CursorAssist libraries:
  Canon            → (nothing)         Schemas + DTOs (leaf)
  Trace            → (nothing)         Input recording (leaf)
  Policy           → Canon             Profile-to-config mapping
  Engine           → Canon, Trace      Transform pipeline

  Runtime.Core     → Engine, Policy    Thread management, config swap
  Runtime.Windows  → Runtime.Core      Win32 hooks, raw input

MouseTrainer libraries:
  Domain           → (nothing)         RNG, events, run identity (leaf)
  Simulation       → Domain            Game loop, mutators, levels
  Audio            → Domain            Cue system, asset verification

Apps:
  CursorAssist.Pilot       → all CursorAssist libs    Tray-based assistant
  MouseTrainer.MauiHost    → all MouseTrainer libs     MAUI desktop game
```

## Two products, one workspace

The repository contains two products sharing design principles but not code:

- **CursorAssist** — real-time cursor assistance for people with motor impairments
- **MouseTrainer** — dexterity training game for building the skills to need less assistance over time

## Design principles

- **Determinism is constitutional.** Same input produces the same output, always. No `DateTime.Now`, no `Random`, no platform-dependent floats in the hot path.
- **DSP-grounded, not ad hoc.** EMA cutoff frequencies from closed-form formulas. Power-law frequency weighting. Velocity-attenuated phase compensation.
- **Modular with enforced boundaries.** One-way dependencies, no cycles. Canon and Trace are leaves. Apps are composition roots.
- **Protocol-grade identity.** FNV-1a hashing with canonical parameter serialization. xorshift32 RNG for reproducible game sessions.
- **Accessibility is the product.** CursorAssist exists to make computers usable for people with motor impairments.
