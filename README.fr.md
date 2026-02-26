<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.md">English</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/cursor-assist/readme.jpg" width="400" />
</p>

<p align="center">
  <a href="https://github.com/mcp-tool-shop-org/CursorAssist/actions/workflows/ci.yml"><img src="https://github.com/mcp-tool-shop-org/CursorAssist/actions/workflows/ci.yml/badge.svg" alt="CI" /></a>
  <a href="https://github.com/mcp-tool-shop-org/CursorAssist/blob/main/LICENSE"><img src="https://img.shields.io/github/license/mcp-tool-shop-org/CursorAssist" alt="License: MIT" /></a>
  <a href="https://mcp-tool-shop-org.github.io/CursorAssist/"><img src="https://img.shields.io/badge/Landing_Page-live-blue" alt="Landing Page" /></a>
</p>

**Un moteur déterministe pour le contrôle assisté du curseur, l'évaluation de l'accessibilité de l'interface utilisateur et la formation adaptative des compétences motrices.**

---

## Pourquoi CursorAssist ?

- **Compensation des tremblements qui fonctionne réellement.** Lissage EMA basé sur le traitement numérique du signal, avec un facteur alpha adaptatif à la vitesse, un filtre coupe-fréquence sensible à la fréquence, une option de double filtre (-40 dB/décade) et une compensation de phase – et non un simple filtre passe-bas.
- **Déterministe par conception.** La même entrée produit toujours la même sortie. Pas de `DateTime.Now`, pas de `Random`, pas de nombres à virgule flottante dépendants de la plateforme. Chaque image est reproductible et vérifiable via des chaînes de hachage FNV-1a.
- **Paquets NuGet modulaires.** Utilisez uniquement les schémas (Canon), uniquement le format de trace (Trace), uniquement le mappeur de politiques (Policy), ou l'ensemble du pipeline de transformation (Engine). Pas de couplage forcé.
- **Profilage moteur avec des mathématiques DSP réelles.** Zones mort pondérées par une loi de puissance en fonction de la fréquence, calcul EMA de la fréquence de coupure basé sur la fréquence des tremblements, détection de l'intention directionnelle via la cohérence cosinus, et hystérésis pour chaque limite d'engagement.
- **Plus de 214 tests.** Chaque transformation, chaque règle de politique, chaque cas limite.

---

## Deux interfaces utilisateur, un moteur

Cet espace de travail contient deux produits qui partagent un cœur déterministe commun :

### CursorAssist – Assistance au curseur en temps réel

Pour les personnes souffrant de troubles moteurs (tremblements, amplitude de mouvement limitée, fatigue). Fonctionne comme une application dans la barre d'état système qui intercepte et transforme les entrées brutes du pointeur en temps réel.

- Compensation des tremblements via un lissage EMA adaptatif à la vitesse et une correction de phase.
- Zones mort douces adaptatives avec compression quadratique (pas de bords nets).
- Résistance aux bords et magnétisme de la cible avec hystérésis.
- Profilage moteur avec schémas versionnés.
- Cartographie de politiques déterministe : le même profil produit toujours la même configuration.

### MouseTrainer – Jeu déterministe pour améliorer la dextérité du curseur

Pour développer la dextérité nécessaire pour réduire l'assistance au fil du temps. Un jeu de bureau .NET MAUI avec une simulation à pas fixe.

- Simulation à 60 Hz avec des mutateurs de blueprint composables.
- Identité de session stable sur toutes les plateformes grâce à un générateur de nombres aléatoires xorshift32 et au hachage FNV-1a.
- Mode "Gauntlet" par glisser-déposer pour l'entraînement aux tâches du curseur dans le monde réel.
- Système de signaux audio piloté par des événements avec un jitter de volume/hauteur de ton déterministe.

---

## Paquets NuGet

Les quatre bibliothèques de CursorAssist sont publiées sur NuGet en tant que paquets indépendants :

| Paquet | NuGet | Description |
| --------- | ------- | ------------- |
| **CursorAssist.Canon** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Canon)](https://www.nuget.org/packages/CursorAssist.Canon) | Schémas et DTO immuables et versionnés pour les profils moteurs, les configurations d'assistance, les plans de difficulté et les rapports d'accessibilité. Zéro dépendance. |
| **CursorAssist.Trace** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Trace)](https://www.nuget.org/packages/CursorAssist.Trace) | Format de trace JSONL (`.castrace.jsonl`) pour l'enregistrement et la lecture des entrées du curseur. Lecteur/écrivain thread-safe. Zéro dépendance. |
| **CursorAssist.Policy** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Policy)](https://www.nuget.org/packages/CursorAssist.Policy) | Mappeur déterministe des profils moteurs vers les configurations d'assistance. Compensation des tremblements basée sur le traitement numérique du signal avec des formules de coupure EMA, des zones mort pondérées par une loi de puissance et une compensation de phase. Dépend de Canon. |
| **CursorAssist.Engine** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Engine)](https://www.nuget.org/packages/CursorAssist.Engine) | Pipeline de transformation des entrées avec un accumulateur à 60 Hz, une chaîne `IInputTransform` composable et une collecte de métriques. Dépend de Canon et de Trace. |

### Installation

```bash
# Install individual packages
dotnet add package CursorAssist.Canon
dotnet add package CursorAssist.Trace
dotnet add package CursorAssist.Policy
dotnet add package CursorAssist.Engine
```

Ou ajoutez-le à votre fichier `.csproj` :

```xml
<PackageReference Include="CursorAssist.Canon" Version="1.0.0" />
<PackageReference Include="CursorAssist.Trace" Version="1.0.0" />
<PackageReference Include="CursorAssist.Policy" Version="1.0.0" />
<PackageReference Include="CursorAssist.Engine" Version="1.0.0" />
```

> Vous n'avez besoin que des paquets que vous utilisez. Canon et Trace n'ont aucune dépendance. Policy dépend de Canon. Engine dépend de Canon et de Trace.

---

## Démarrage rapide

### Associez un profil moteur à une configuration d'assistance (Policy)

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

### Construisez et exécutez le pipeline de transformation (Engine)

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

### Enregistrez une trace (Trace)

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

### Ordre du pipeline de transformation

```
Raw Input --> SoftDeadzone --> Smoothing --> PhaseCompensation --> DirectionalIntent --> TargetMagnetism --> Output
```

Chaque transformation implémente l'interface `IInputTransform` et est assemblée via `TransformPipeline`. La classe `DeterministicPipeline` encapsule la chaîne dans une boucle d'accumulation à pas fixe, avec une vérification de hachage FNV-1a à chaque itération.

---

## Principes de conception

- **Le déterminisme est fondamental.** Les mêmes entrées produisent toujours les mêmes sorties. Pas de `DateTime.Now`, pas de `Random`, pas de nombres à virgule flottante dépendant de la plateforme dans les parties critiques du code. Chaque image est vérifiée par hachage.
- **Basé sur les principes du traitement numérique du signal (DSP), et non sur des solutions ad hoc.** Les fréquences de coupure de l'EMA (moyenne mobile exponentielle) sont dérivées de formules analytiques (`fc = alpha * Fs / 2pi`). Les rayons des zones mortes utilisent une pondération fréquentielle en loi de puissance. La compensation de phase est atténuée en fonction de la vitesse pour éviter les dépassements.
- **Modulaire avec des limites clairement définies.** Dépendances unidirectionnelles, pas de cycles. Canon et Trace sont des éléments terminaux. Les applications sont les points de départ de l'assemblage.
- **Identité de qualité professionnelle.** Les identifiants sont permanents et immuables. Hachage FNV-1a avec sérialisation des paramètres canoniques. Générateur de nombres aléatoires xorshift32 pour des sessions de jeu reproductibles.
- **L'accessibilité est au cœur du produit.** CursorAssist a été conçu pour rendre les ordinateurs utilisables par les personnes ayant des troubles moteurs. MouseTrainer a été conçu pour aider les utilisateurs à développer la dextérité afin de nécessiter moins d'assistance au fil du temps.

---

## Construction à partir du code source

### Prérequis

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) ou version ultérieure
- Windows 10/11 (pour Runtime.Windows et MouseTrainer.MauiHost)
- Tout système d'exploitation pour les bibliothèques de base (Canon, Trace, Policy, Engine sont indépendants de la plateforme)

### Construction

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

### Création des packages NuGet localement

```bash
dotnet pack src/CursorAssist.Canon/CursorAssist.Canon.csproj -c Release -o ./nupkg
dotnet pack src/CursorAssist.Trace/CursorAssist.Trace.csproj -c Release -o ./nupkg
dotnet pack src/CursorAssist.Policy/CursorAssist.Policy.csproj -c Release -o ./nupkg
dotnet pack src/CursorAssist.Engine/CursorAssist.Engine.csproj -c Release -o ./nupkg
```

---

## Structure du projet

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

## Licence

[MIT](LICENSE)

---

<p align="center">
  Built by <a href="https://mcp-tool-shop.github.io/">MCP Tool Shop</a>
</p>
