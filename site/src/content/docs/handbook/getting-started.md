---
title: Getting Started
description: Install CursorAssist packages and build from source.
sidebar:
  order: 1
---

CursorAssist is a deterministic engine for assistive cursor control, with four modular NuGet packages.

## Prerequisites

- .NET 8 SDK or later
- Windows 10/11 (for Runtime.Windows and MouseTrainer.MauiHost)
- Any OS for the core libraries (Canon, Trace, Policy, Engine are platform-agnostic)

## Install from NuGet

```bash
# Individual packages — use only what you need
dotnet add package CursorAssist.Canon     # Schemas + DTOs (leaf)
dotnet add package CursorAssist.Trace     # JSONL trace format (leaf)
dotnet add package CursorAssist.Policy    # Profile-to-config mapper
dotnet add package CursorAssist.Engine    # Transform pipeline
```

Or add to your `.csproj`:

```xml
<PackageReference Include="CursorAssist.Canon" Version="1.0.3" />
<PackageReference Include="CursorAssist.Trace" Version="1.0.3" />
<PackageReference Include="CursorAssist.Policy" Version="1.0.3" />
<PackageReference Include="CursorAssist.Engine" Version="1.0.3" />
```

## Build from source

```bash
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

## Pack NuGet packages locally

```bash
dotnet pack src/CursorAssist.Canon/CursorAssist.Canon.csproj -c Release -o ./nupkg
dotnet pack src/CursorAssist.Trace/CursorAssist.Trace.csproj -c Release -o ./nupkg
dotnet pack src/CursorAssist.Policy/CursorAssist.Policy.csproj -c Release -o ./nupkg
dotnet pack src/CursorAssist.Engine/CursorAssist.Engine.csproj -c Release -o ./nupkg
```
