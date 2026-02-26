<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.md">English</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/cursor-assist/readme.jpg" width="400" />
</p>

<p align="center">
  <a href="https://github.com/mcp-tool-shop-org/CursorAssist/actions/workflows/ci.yml"><img src="https://github.com/mcp-tool-shop-org/CursorAssist/actions/workflows/ci.yml/badge.svg" alt="CI" /></a>
  <a href="https://github.com/mcp-tool-shop-org/CursorAssist/blob/main/LICENSE"><img src="https://img.shields.io/github/license/mcp-tool-shop-org/CursorAssist" alt="License: MIT" /></a>
  <a href="https://mcp-tool-shop-org.github.io/CursorAssist/"><img src="https://img.shields.io/badge/Landing_Page-live-blue" alt="Landing Page" /></a>
</p>

**Un motor determinista para el control asistido del cursor, la evaluación de la accesibilidad de la interfaz de usuario y el entrenamiento adaptativo de habilidades motoras.**

---

## ¿Por qué CursorAssist?

- **Compensación de temblores que realmente funciona.** Suavizado EMA basado en procesamiento digital de señales (DSP) con un factor alfa adaptable a la velocidad, un filtro de corte con conocimiento de la frecuencia, compensación de fase opcional (dual-pole, -40 dB/década) y compensación de fase; no es un simple filtro de paso bajo.
- **Determinista por diseño.** La misma entrada siempre produce la misma salida. No utiliza `DateTime.Now`, ni `Random`, ni números de punto flotante dependientes de la plataforma. Cada fotograma es reproducible y verificable mediante cadenas de hash FNV-1a.
- **Paquetes NuGet modulares.** Puede usar solo los esquemas (Canon), solo el formato de seguimiento (Trace), solo el mapeador de políticas (Policy) o la canalización de transformación completa (Engine). No hay acoplamiento forzado.
- **Perfilado del motor con matemáticas de DSP reales.** Zonas muertas ponderadas por frecuencia con ley de potencia, cálculo directo del filtro de corte EMA a partir de la frecuencia del temblor, detección de la intención direccional mediante coherencia coseno e histéresis en cada límite de activación.
- **Más de 214 pruebas.** Cada transformación, cada regla de política, cada caso límite.

---

## Dos interfaces de producto, un motor

Este espacio de trabajo contiene dos productos que comparten un núcleo determinista común:

### CursorAssist: Asistencia del cursor en tiempo real

Para personas con discapacidades motoras (temblores, rango limitado, fatiga). Se ejecuta como una aplicación en la bandeja del sistema que intercepta y transforma la entrada bruta del puntero en tiempo real.

- Compensación de temblores mediante suavizado EMA adaptable a la velocidad y corrección de fase.
- Zonas muertas adaptables con compresión cuadrática (sin bordes duros).
- Resistencia a los bordes y magnetismo del objetivo con histéresis.
- Perfilado del motor con esquemas versionados.
- Mapeo de políticas determinista: el mismo perfil siempre produce la misma configuración.

### MouseTrainer: Juego determinista para mejorar la destreza del cursor

Para desarrollar la destreza necesaria para requerir menos asistencia con el tiempo. Un juego de escritorio .NET MAUI con simulación de paso fijo.

- Simulación de 60 Hz con mutadores de esquema composables.
- Identidad de ejecución estable en la plataforma mediante el generador de números aleatorios xorshift32 y el hash FNV-1a.
- Modo "Gauntlet" de arrastrar y soltar para el entrenamiento de tareas del cursor en el mundo real.
- Sistema de señales de audio basado en eventos con jitter determinista de volumen/tono.

---

## Paquetes NuGet

Las cuatro bibliotecas de CursorAssist se publican en NuGet como paquetes independientes:

| Paquete | NuGet | Descripción |
| --------- | ------- | ------------- |
| **CursorAssist.Canon** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Canon)](https://www.nuget.org/packages/CursorAssist.Canon) | Esquemas y DTOs inmutables y versionados para perfiles de motor, configuraciones de asistencia, planes de dificultad e informes de accesibilidad. Sin dependencias. |
| **CursorAssist.Trace** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Trace)](https://www.nuget.org/packages/CursorAssist.Trace) | Formato de seguimiento JSONL (`.castrace.jsonl`) para la grabación y reproducción de la entrada del cursor. Escritor/lector compatible con múltiples hilos. Sin dependencias. |
| **CursorAssist.Policy** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Policy)](https://www.nuget.org/packages/CursorAssist.Policy) | Mapeador determinista de perfiles de motor a configuraciones de asistencia. Compensación de temblores basada en DSP con fórmulas de corte EMA, zonas muertas con ley de potencia y compensación de fase. Depende de Canon. |
| **CursorAssist.Engine** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Engine)](https://www.nuget.org/packages/CursorAssist.Engine) | Canalización de transformación de entrada con acumulador de 60 Hz, cadena composable de `IInputTransform` y recopilación de métricas. Depende de Canon y Trace. |

### Instalar

```bash
# Install individual packages
dotnet add package CursorAssist.Canon
dotnet add package CursorAssist.Trace
dotnet add package CursorAssist.Policy
dotnet add package CursorAssist.Engine
```

O agregar a su `.csproj`:

```xml
<PackageReference Include="CursorAssist.Canon" Version="1.0.0" />
<PackageReference Include="CursorAssist.Trace" Version="1.0.0" />
<PackageReference Include="CursorAssist.Policy" Version="1.0.0" />
<PackageReference Include="CursorAssist.Engine" Version="1.0.0" />
```

> Solo necesita los paquetes que utiliza. Canon y Trace no tienen dependencias. Policy depende de Canon. Engine depende de Canon y Trace.

---

## Comienzo rápido

### Mapear un perfil de motor a una configuración de asistencia (Policy)

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

### Construir y ejecutar la canalización de transformación (Engine)

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

### Grabar un seguimiento (Trace)

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

## Arquitectura

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

### Orden de la canalización de transformación

```
Raw Input --> SoftDeadzone --> Smoothing --> PhaseCompensation --> DirectionalIntent --> TargetMagnetism --> Output
```

Cada transformación implementa la interfaz `IInputTransform` y se compone a través de `TransformPipeline`. `DeterministicPipeline` encapsula la cadena en un bucle acumulador de pasos fijos, con verificación de hash FNV-1a en cada iteración.

---

## Principios de Diseño

- **El determinismo es fundamental.** La misma entrada siempre produce la misma salida. No se utilizan `DateTime.Now`, ni `Random`, ni números de punto flotante dependientes de la plataforma en las partes críticas del código. Cada fotograma se verifica mediante un hash.
- **Basado en principios de procesamiento de señales (DSP), no en soluciones ad hoc.** Las frecuencias de corte de la media móvil exponencial (EMA) se derivan de fórmulas cerradas (`fc = alpha * Fs / 2pi`). Los radios de la zona muerta utilizan ponderación de frecuencia por ley de potencia. La compensación de fase se atenúa según la velocidad para evitar el sobreimpulso.
- **Modular con límites definidos.** Dependencias unidireccionales, sin ciclos. Canon y Trace son elementos finales. Las aplicaciones son los puntos de composición.
- **Identidad de grado de protocolo.** Los identificadores son permanentes e inmutables. Se utiliza el hashing FNV-1a con serialización de parámetros canónicos. Se utiliza el generador de números aleatorios xorshift32 para sesiones de juego reproducibles.
- **La accesibilidad es el objetivo.** CursorAssist existe para hacer que las computadoras sean utilizables para personas con discapacidades motoras. MouseTrainer existe para ayudar a las personas a desarrollar la destreza necesaria para requerir menos asistencia con el tiempo.

---

## Compilación desde el código fuente

### Requisitos previos

- [SDK de .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0) o posterior
- Windows 10/11 (para Runtime.Windows y MouseTrainer.MauiHost)
- Cualquier sistema operativo para las bibliotecas principales (Canon, Trace, Policy, Engine son independientes de la plataforma)

### Compilación

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

### Empaquetar paquetes NuGet localmente

```bash
dotnet pack src/CursorAssist.Canon/CursorAssist.Canon.csproj -c Release -o ./nupkg
dotnet pack src/CursorAssist.Trace/CursorAssist.Trace.csproj -c Release -o ./nupkg
dotnet pack src/CursorAssist.Policy/CursorAssist.Policy.csproj -c Release -o ./nupkg
dotnet pack src/CursorAssist.Engine/CursorAssist.Engine.csproj -c Release -o ./nupkg
```

---

## Estructura del proyecto

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

## Licencia

[MIT](LICENSE)

---

<p align="center">
  Built by <a href="https://mcp-tool-shop.github.io/">MCP Tool Shop</a>
</p>
