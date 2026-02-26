<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.md">English</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/cursor-assist/readme.jpg" width="400" />
</p>

<p align="center">
  <a href="https://github.com/mcp-tool-shop-org/CursorAssist/actions/workflows/ci.yml"><img src="https://github.com/mcp-tool-shop-org/CursorAssist/actions/workflows/ci.yml/badge.svg" alt="CI" /></a>
  <a href="https://github.com/mcp-tool-shop-org/CursorAssist/blob/main/LICENSE"><img src="https://img.shields.io/github/license/mcp-tool-shop-org/CursorAssist" alt="License: MIT" /></a>
  <a href="https://mcp-tool-shop-org.github.io/CursorAssist/"><img src="https://img.shields.io/badge/Landing_Page-live-blue" alt="Landing Page" /></a>
</p>

**Un motore deterministico per il controllo assistito del cursore, la valutazione dell'accessibilità dell'interfaccia utente e l'addestramento adattivo delle capacità motorie.**

---

## Perché CursorAssist?

- **Compensazione del tremore che funziona realmente.** Smussamento EMA basato su elaborazione del segnale digitale (DSP) con un fattore alfa adattabile alla velocità, un filtro passa-alto con frequenza regolabile, un filtro opzionale a doppia polarità (-40 dB/decennio) e compensazione di fase: non un semplice filtro passa-basso.
- **Progettato per essere deterministico.** Lo stesso input produce sempre lo stesso output. Nessun riferimento a `DateTime.Now`, nessuna funzione `Random`, nessuna variabile in virgola mobile dipendente dalla piattaforma. Ogni frame è riproducibile e verificabile tramite catene di hash FNV-1a.
- **Pacchetti NuGet modulari.** È possibile utilizzare solo gli schemi (Canon), solo il formato di traccia (Trace), solo il mapper delle policy (Policy) o l'intera pipeline di trasformazione (Engine). Nessuna dipendenza forzata.
- **Profilazione motoria con matematica DSP reale.** Zone morte ponderate per frequenza secondo una legge di potenza, calcolo EMA del cutoff derivato dalla frequenza del tremore, rilevamento dell'intento direzionale tramite coerenza coseno e isteresi su ogni limite di attivazione.
- **Oltre 214 test.** Ogni trasformazione, ogni regola di policy, ogni caso limite.

---

## Due interfacce utente, un unico motore

Questo ambiente di lavoro contiene due prodotti che condividono un nucleo deterministico comune:

### CursorAssist: Assistenza al cursore in tempo reale

Per persone con disturbi motori (tremore, mobilità limitata, affaticamento). Funziona come un'applicazione nella barra delle applicazioni che intercetta e trasforma l'input del puntatore in tempo reale.

- Compensazione del tremore tramite smussamento EMA adattabile alla velocità e correzione di fase.
- Zone morte adattive con compressione quadratica (senza bordi netti).
- Resistenza ai bordi e magnetismo del bersaglio con isteresi.
- Profilazione motoria con schemi versionati.
- Mappatura delle policy deterministica: lo stesso profilo produce sempre la stessa configurazione.

### MouseTrainer: Gioco per migliorare la destrezza del cursore

Per sviluppare la destrezza necessaria per ridurre gradualmente l'assistenza. Un gioco desktop .NET MAUI con simulazione a intervallo fisso.

- Simulazione a 60 Hz con mutatori di blueprint componibili.
- Identità di esecuzione stabile a livello di piattaforma tramite RNG xorshift32 e hashing FNV-1a.
- Modalità "Gauntlet" con trascinamento e rilascio per l'addestramento in scenari reali.
- Sistema di segnali audio basato su eventi con jitter deterministico del volume/pitch.

---

## Pacchetti NuGet

Le quattro librerie di CursorAssist sono pubblicate su NuGet come pacchetti indipendenti:

| Pacchetto | NuGet | Descrizione |
| --------- | ------- | ------------- |
| **CursorAssist.Canon** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Canon)](https://www.nuget.org/packages/CursorAssist.Canon) | Schemi e DTO immutabili e versionati per profili motori, configurazioni di assistenza, piani di difficoltà e report di accessibilità. Nessuna dipendenza. |
| **CursorAssist.Trace** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Trace)](https://www.nuget.org/packages/CursorAssist.Trace) | Formato di traccia JSONL (`.castrace.jsonl`) per la registrazione e la riproduzione dell'input del cursore. Scrittore/lettore thread-safe. Nessuna dipendenza. |
| **CursorAssist.Policy** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Policy)](https://www.nuget.org/packages/CursorAssist.Policy) | Mapper deterministico da profili motori a configurazioni di assistenza. Compensazione del tremore basata su elaborazione del segnale digitale (DSP) con formule di cutoff EMA, zone morte secondo una legge di potenza e compensazione di fase. Dipende da Canon. |
| **CursorAssist.Engine** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Engine)](https://www.nuget.org/packages/CursorAssist.Engine) | Pipeline di trasformazione dell'input con accumulatore a 60 Hz, catena componibile di `IInputTransform` e raccolta di metriche. Dipende da Canon e Trace. |

### Installazione

```bash
# Install individual packages
dotnet add package CursorAssist.Canon
dotnet add package CursorAssist.Trace
dotnet add package CursorAssist.Policy
dotnet add package CursorAssist.Engine
```

Oppure aggiungi al tuo file `.csproj`:

```xml
<PackageReference Include="CursorAssist.Canon" Version="1.0.0" />
<PackageReference Include="CursorAssist.Trace" Version="1.0.0" />
<PackageReference Include="CursorAssist.Policy" Version="1.0.0" />
<PackageReference Include="CursorAssist.Engine" Version="1.0.0" />
```

> Hai bisogno solo dei pacchetti che utilizzi. Canon e Trace non hanno dipendenze. Policy dipende da Canon. Engine dipende da Canon e Trace.

---

## Guida rapida

### Mappa un profilo motorio a una configurazione di assistenza (Policy)

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

### Costruisci ed esegui la pipeline di trasformazione (Engine)

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

### Registra una traccia (Trace)

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

## Architettura

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

### Ordine della pipeline di trasformazione

```
Raw Input --> SoftDeadzone --> Smoothing --> PhaseCompensation --> DirectionalIntent --> TargetMagnetism --> Output
```

Ogni trasformazione implementa l'interfaccia `IInputTransform` e viene composta tramite `TransformPipeline`. La classe `DeterministicPipeline` avvolge la sequenza in un ciclo di accumulo a intervalli fissi, con verifica dell'hash FNV-1a ad ogni iterazione.

---

## Principi di progettazione

- **Il determinismo è fondamentale.** Lo stesso input produce sempre lo stesso output. Nessun utilizzo di `DateTime.Now`, nessun utilizzo di `Random`, e nessuna variabile in virgola mobile dipendente dalla piattaforma nel codice critico. Ogni frame è verificato tramite hash.
- **Basato su principi di elaborazione del segnale, non su soluzioni ad hoc.** Le frequenze di cutoff dell'EMA (Exponential Moving Average) sono derivate da formule analitiche (`fc = alpha * Fs / 2pi`). I raggi delle zone morte utilizzano una ponderazione di frequenza basata su leggi di potenza. La compensazione di fase è attenuata in base alla velocità per prevenire overshoot.
- **Modularità con confini ben definiti.** Dipendenze unidirezionali, senza cicli. Canon e Trace sono componenti finali. Le applicazioni sono i punti di partenza della composizione.
- **Identità di livello protocollo.** Gli ID sono permanenti e immutabili. Hashing FNV-1a con serializzazione dei parametri canonici. Generatore di numeri casuali xorshift32 per sessioni di gioco riproducibili.
- **L'accessibilità è il prodotto.** CursorAssist è progettato per rendere i computer utilizzabili da persone con disabilità motorie. MouseTrainer è progettato per aiutare le persone a sviluppare la destrezza necessaria per ridurre la necessità di assistenza nel tempo.

---

## Compilazione dal codice sorgente

### Prerequisiti

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) o versione successiva
- Windows 10/11 (per Runtime.Windows e MouseTrainer.MauiHost)
- Qualsiasi sistema operativo per le librerie principali (Canon, Trace, Policy, Engine sono indipendenti dalla piattaforma)

### Compilazione

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

### Creazione dei pacchetti NuGet localmente

```bash
dotnet pack src/CursorAssist.Canon/CursorAssist.Canon.csproj -c Release -o ./nupkg
dotnet pack src/CursorAssist.Trace/CursorAssist.Trace.csproj -c Release -o ./nupkg
dotnet pack src/CursorAssist.Policy/CursorAssist.Policy.csproj -c Release -o ./nupkg
dotnet pack src/CursorAssist.Engine/CursorAssist.Engine.csproj -c Release -o ./nupkg
```

---

## Struttura del progetto

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

## Licenza

[MIT](LICENSE)

---

<p align="center">
  Built by <a href="https://mcp-tool-shop.github.io/">MCP Tool Shop</a>
</p>
