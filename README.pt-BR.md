<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.md">English</a>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/cursor-assist/readme.jpg" width="400" />
</p>

<p align="center">
  <a href="https://github.com/mcp-tool-shop-org/CursorAssist/actions/workflows/ci.yml"><img src="https://github.com/mcp-tool-shop-org/CursorAssist/actions/workflows/ci.yml/badge.svg" alt="CI" /></a>
  <a href="https://github.com/mcp-tool-shop-org/CursorAssist/blob/main/LICENSE"><img src="https://img.shields.io/github/license/mcp-tool-shop-org/CursorAssist" alt="License: MIT" /></a>
  <a href="https://mcp-tool-shop-org.github.io/CursorAssist/"><img src="https://img.shields.io/badge/Landing_Page-live-blue" alt="Landing Page" /></a>
</p>

**Um motor determinístico para controle de cursor assistivo, avaliação de acessibilidade de interfaces e treinamento adaptativo de habilidades motoras.**

---

## Por que CursorAssist?

- **Compensação de tremor que realmente funciona.** Suavização EMA baseada em processamento digital de sinais (DSP), com ajuste adaptativo da taxa de aprendizado (alpha), filtro de corte com sensibilidade à frequência, compensação de fase opcional (dual-pole, -40 dB/década) – não é um simples filtro passa-baixa.
- **Determinístico por design.** A mesma entrada sempre produz a mesma saída. Sem `DateTime.Now`, sem `Random`, sem números de ponto flutuante dependentes da plataforma. Cada quadro é reproduzível e verificável através de cadeias de hash FNV-1a.
- **Pacotes NuGet modulares.** Use apenas os esquemas (Canon), apenas o formato de rastreamento (Trace), apenas o mapeador de políticas (Policy) ou todo o pipeline de transformação (Engine). Sem acoplamento forçado.
- **Perfil de motor com matemática de DSP real.** Zonas mortas ponderadas por frequência em lei de potência, cálculo fechado da frequência de corte EMA a partir da frequência de tremor, detecção de intenção direcional via coerência de cosseno e histerese em cada limite de ativação.
- **Mais de 214 testes.** Cada transformação, cada regra de política, cada caso limite.

---

## Duas Interfaces de Produto, Um Motor

Este ambiente de trabalho contém dois produtos que compartilham um núcleo determinístico comum:

### CursorAssist – Assistência de Cursor em Tempo Real

Para pessoas com deficiências motoras (tremor, amplitude de movimento limitada, fadiga). Funciona como um aplicativo na bandeja do sistema que intercepta e transforma a entrada bruta do cursor em tempo real.

- Compensação de tremor via suavização EMA adaptativa à velocidade e correção de fase.
- Zonas mortas adaptativas com compressão quadrática (sem bordas rígidas).
- Resistência nas bordas e magnetismo do alvo com histerese.
- Perfil de motor com esquemas versionados.
- Mapeamento de políticas determinístico: o mesmo perfil sempre produz a mesma configuração.

### MouseTrainer – Jogo Determinístico para Destreza do Cursor

Para desenvolver a destreza necessária para reduzir a necessidade de assistência ao longo do tempo. Um jogo para desktop .NET MAUI com simulação de passo fixo.

- Simulação de 60 Hz com mutadores de blueprint compostáveis.
- Identidade de execução estável em diferentes plataformas via RNG xorshift32 e hashing FNV-1a.
- Modo Gauntlet de arrastar e soltar para treinamento de tarefas de cursor no mundo real.
- Sistema de sinalização de áudio baseado em eventos com jitter determinístico de volume/tom.

---

## Pacotes NuGet

As quatro bibliotecas do CursorAssist são publicadas no NuGet como pacotes independentes:

| Pacote | NuGet | Descrição |
| --------- | ------- | ------------- |
| **CursorAssist.Canon** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Canon)](https://www.nuget.org/packages/CursorAssist.Canon) | Esquemas e DTOs imutáveis e versionados para perfis de motor, configurações de assistência, planos de dificuldade e relatórios de acessibilidade. Sem dependências. |
| **CursorAssist.Trace** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Trace)](https://www.nuget.org/packages/CursorAssist.Trace) | Formato de rastreamento JSONL (`.castrace.jsonl`) para gravação e reprodução de entrada do cursor. Leitor/escritor thread-safe. Sem dependências. |
| **CursorAssist.Policy** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Policy)](https://www.nuget.org/packages/CursorAssist.Policy) | Mapeador determinístico de perfis de motor para configurações de assistência. Compensação de tremor baseada em DSP com fórmulas de corte EMA, zonas mortas em lei de potência e compensação de fase. Depende de Canon. |
| **CursorAssist.Engine** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Engine)](https://www.nuget.org/packages/CursorAssist.Engine) | Pipeline de transformação de entrada com acumulador de 60 Hz, cadeia compostável de `IInputTransform` e coleta de métricas. Depende de Canon e Trace. |

### Instalar

```bash
# Install individual packages
dotnet add package CursorAssist.Canon
dotnet add package CursorAssist.Trace
dotnet add package CursorAssist.Policy
dotnet add package CursorAssist.Engine
```

Ou adicione ao seu `.csproj`:

```xml
<PackageReference Include="CursorAssist.Canon" Version="1.0.0" />
<PackageReference Include="CursorAssist.Trace" Version="1.0.0" />
<PackageReference Include="CursorAssist.Policy" Version="1.0.0" />
<PackageReference Include="CursorAssist.Engine" Version="1.0.0" />
```

> Você só precisa dos pacotes que usa. Canon e Trace não têm dependências. Policy depende de Canon. Engine depende de Canon e Trace.

---

## Início Rápido

### Mapeie um perfil de motor para uma configuração de assistência (Policy)

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

### Construa e execute o pipeline de transformação (Engine)

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

### Grave um rastreamento (Trace)

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

## Arquitetura

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

### Ordem do Pipeline de Transformação

```
Raw Input --> SoftDeadzone --> Smoothing --> PhaseCompensation --> DirectionalIntent --> TargetMagnetism --> Output
```

Cada transformação implementa a interface `IInputTransform` e é composta através de `TransformPipeline`. A `DeterministicPipeline` envolve a sequência em um loop de acumulação com um intervalo de tempo fixo, com verificação de hash FNV-1a em cada iteração.

---

## Princípios de Design

- **O determinismo é fundamental.** A mesma entrada sempre produz a mesma saída. Não há uso de `DateTime.Now`, `Random` ou números de ponto flutuante dependentes da plataforma no código crítico. Cada quadro é verificado por hash.
- **Baseado em princípios de processamento de sinais (DSP), não em soluções improvisadas.** As frequências de corte da média móvel (EMA) são derivadas de fórmulas matemáticas (`fc = alpha * Fs / 2pi`). Os raios da zona morta utilizam ponderação de frequência por lei de potência. A compensação de fase é atenuada pela velocidade para evitar oscilações.
- **Modular, com limites bem definidos.** Dependências unidirecionais, sem ciclos. Canon e Trace são componentes básicos. As aplicações são os pontos de partida da composição.
- **Identidade de nível de protocolo.** Os IDs são permanentes e imutáveis. Hashing FNV-1a com serialização de parâmetros canônicos. Gerador de números aleatórios xorshift32 para sessões de jogo reproduzíveis.
- **A acessibilidade é o produto.** O CursorAssist existe para tornar os computadores utilizáveis para pessoas com deficiências motoras. O MouseTrainer existe para ajudar as pessoas a desenvolver a destreza necessária para precisar de menos assistência com o tempo.

---

## Construção a partir do código fonte

### Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) ou posterior
- Windows 10/11 (para Runtime.Windows e MouseTrainer.MauiHost)
- Qualquer sistema operacional para as bibliotecas principais (Canon, Trace, Policy, Engine são independentes de plataforma)

### Construção

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

### Empacotar pacotes NuGet localmente

```bash
dotnet pack src/CursorAssist.Canon/CursorAssist.Canon.csproj -c Release -o ./nupkg
dotnet pack src/CursorAssist.Trace/CursorAssist.Trace.csproj -c Release -o ./nupkg
dotnet pack src/CursorAssist.Policy/CursorAssist.Policy.csproj -c Release -o ./nupkg
dotnet pack src/CursorAssist.Engine/CursorAssist.Engine.csproj -c Release -o ./nupkg
```

---

## Estrutura do projeto

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

## Licença

[MIT](LICENSE)

---

<p align="center">
  Built by <a href="https://mcp-tool-shop.github.io/">MCP Tool Shop</a>
</p>
