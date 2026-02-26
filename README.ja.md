<p align="center">
  <a href="README.md">English</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/cursor-assist/readme.jpg" width="400" />
</p>

<p align="center">
  <a href="https://github.com/mcp-tool-shop-org/CursorAssist/actions/workflows/ci.yml"><img src="https://github.com/mcp-tool-shop-org/CursorAssist/actions/workflows/ci.yml/badge.svg" alt="CI" /></a>
  <a href="https://github.com/mcp-tool-shop-org/CursorAssist/blob/main/LICENSE"><img src="https://img.shields.io/github/license/mcp-tool-shop-org/CursorAssist" alt="License: MIT" /></a>
  <a href="https://mcp-tool-shop-org.github.io/CursorAssist/"><img src="https://img.shields.io/badge/Landing_Page-live-blue" alt="Landing Page" /></a>
</p>

**支援型カーソル制御、UIアクセシビリティのベンチマーク、および適応的な運動スキル訓練のための、決定論的なエンジン。**

---

## なぜCursorAssistなのか？

- **実際に効果のある震え補正機能。** DSPに基づいたEMA平滑化、速度適応型のα値、周波数依存のカットオフ、オプションのデュアルポール（-40 dB/decade）、および位相補正。単なるローパスフィルターではありません。
- **設計上、決定論的。** 同じ入力は常に同じ出力を生成します。 `DateTime.Now`、`Random`、プラットフォーム依存の浮動小数点数はありません。 各フレームは、FNV-1aハッシュチェーンによって再現可能で検証可能です。
- **モジュール式のNuGetパッケージ。** スキーマのみ（Canon）、トレース形式のみ（Trace）、ポリシーマッパーのみ（Policy）、または完全な変換パイプライン（Engine）など、必要なものだけを使用できます。 依存関係は強制されません。
- **実際のDSP数学を使用したモータープロファイリング。** パワーロー周波数加重デッドゾーン、震え周波数からの閉形式EMAカットオフ、コサインコヒーレンスによる方向意図検出、およびすべてのエンゲージメント境界におけるヒステリシス。
- **214以上のテスト。** すべての変換、すべてのポリシールール、すべての例外ケース。

---

## 2つの製品インターフェース、1つのエンジン

このワークスペースには、共通の決定論的なコアを共有する2つの製品があります。

### CursorAssist -- リアルタイムカーソルアシスタンス

運動機能障害（震え、可動範囲の制限、疲労）のある方のために設計されています。 システムトレイアプリケーションとして動作し、生のポインター入力をリアルタイムでインターセプトし、変換します。

- 速度適応型のEMA平滑化および位相補正による震え補正
- 二次圧縮による適応的なソフトデッドゾーン（ハードなエッジなし）
- ヒステリシスによるエッジ抵抗およびターゲット磁力
- バージョン管理されたスキーマを使用したモータープロファイリング
- 決定論的なポリシーマッピング：同じプロファイルは常に同じ構成を生成します。

### MouseTrainer -- 決定論的なカーソルの操作スキルゲーム

時間とともに、アシスタンスなしで作業できるようになるための操作スキルを養うために設計されています。 .NET MAUIを使用したデスクトップゲームで、固定タイムステップシミュレーションを採用しています。

- 固定60Hzシミュレーションと、組み合わせ可能なブループリントミューテーター
- xorshift32 RNGおよびFNV-1aハッシュによるプラットフォーム安定の実行ID
- 実際のカーソルのタスク訓練のためのドラッグアンドドロップガントレットモード
- 決定論的な音量/ピッチのジッタを持つイベント駆動型のオーディオキューシステム

---

## NuGetパッケージ

CursorAssistの4つのライブラリは、独立したパッケージとしてNuGetに公開されています。

| パッケージ | NuGet | 説明 |
| --------- | ------- | ------------- |
| **CursorAssist.Canon** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Canon)](https://www.nuget.org/packages/CursorAssist.Canon) | モータープロファイル、アシスタンス構成、難易度プラン、およびアクセシビリティレポートのバージョン管理された不変のスキーマとDTO。 依存関係はゼロです。 |
| **CursorAssist.Trace** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Trace)](https://www.nuget.org/packages/CursorAssist.Trace) | カーソルの入力の記録と再生のためのJSONLトレース形式（`.castrace.jsonl`）。 スレッドセーフなライター/リーダー。 依存関係はゼロです。 |
| **CursorAssist.Policy** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Policy)](https://www.nuget.org/packages/CursorAssist.Policy) | モータープロファイルからアシスタンス構成への決定論的なマッパー。 EMAカットオフの数式、パワーローデッドゾーン、および位相補正を使用したDSPに基づいた震え補正。 Canonに依存します。 |
| **CursorAssist.Engine** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Engine)](https://www.nuget.org/packages/CursorAssist.Engine) | 60Hzのアキュムレーター、組み合わせ可能な`IInputTransform`チェーン、およびメトリクスの収集を備えた入力変換パイプライン。 CanonとTraceに依存します。 |

### インストール

```bash
# Install individual packages
dotnet add package CursorAssist.Canon
dotnet add package CursorAssist.Trace
dotnet add package CursorAssist.Policy
dotnet add package CursorAssist.Engine
```

または、`.csproj`ファイルに追加します。

```xml
<PackageReference Include="CursorAssist.Canon" Version="1.0.0" />
<PackageReference Include="CursorAssist.Trace" Version="1.0.0" />
<PackageReference Include="CursorAssist.Policy" Version="1.0.0" />
<PackageReference Include="CursorAssist.Engine" Version="1.0.0" />
```

> 使用するパッケージのみが必要です。 CanonとTraceは依存関係のない基本です。 PolicyはCanonに依存します。 EngineはCanonとTraceに依存します。

---

## クイックスタート

### モータープロファイルをアシスタンス構成にマッピングする（Policy）

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

### 変換パイプラインを構築して実行する（Engine）

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

### トレースを記録する（Trace）

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

## アーキテクチャ

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

### 変換パイプラインの順序

```
Raw Input --> SoftDeadzone --> Smoothing --> PhaseCompensation --> DirectionalIntent --> TargetMagnetism --> Output
```

各変換処理は`IInputTransform`インターフェースを実装しており、`TransformPipeline`によって構成されます。`DeterministicPipeline`は、固定時間ステップの累積ループでこれらの処理をラップし、各ステップでFNV-1aハッシュによる検証を行います。

---

## 設計原則

- **決定論が基本です。** 同じ入力に対しては、常に同じ出力が生成されます。`DateTime.Now`や`Random`、ホットパスにおけるプラットフォーム依存の浮動小数点数などは使用しません。すべてのフレームはハッシュ検証されます。
- **DSP（デジタル信号処理）に基づいた設計。** EMA（指数平滑法）のカットオフ周波数は、数式（`fc = alpha * Fs / 2pi`）によって算出されます。デッドゾーンの半径は、べき乗則による周波数重み付けを使用します。位相補正は、オーバーシュートを防ぐために速度に応じて減衰されます。
- **モジュール化されており、境界が明確です。** 一方向の依存関係のみで、循環は存在しません。CanonとTraceは、葉（末端）のコンポーネントです。アプリケーションは、コンポジションのルートです。
- **プロトコルレベルの識別。** IDは永続的で、変更されません。標準パラメータのシリアライズと組み合わせたFNV-1aハッシュを使用します。再現可能なゲームセッションのために、xorshift32乱数生成器を使用します。
- **アクセシビリティが重要です。** CursorAssistは、運動機能に障害のある人がコンピュータを使用できるようにするためのツールです。MouseTrainerは、ユーザーが徐々にサポートなしで操作できるようになるための習熟度を高めるためのツールです。

---

## ソースコードからのビルド

### 前提条件

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)またはそれ以降
- Windows 10/11（Runtime.WindowsおよびMouseTrainer.MauiHostの場合）
- コアライブラリ（Canon、Trace、Policy、Engine）は、どのOSでも動作します。

### ビルド

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

### NuGetパッケージをローカルに作成

```bash
dotnet pack src/CursorAssist.Canon/CursorAssist.Canon.csproj -c Release -o ./nupkg
dotnet pack src/CursorAssist.Trace/CursorAssist.Trace.csproj -c Release -o ./nupkg
dotnet pack src/CursorAssist.Policy/CursorAssist.Policy.csproj -c Release -o ./nupkg
dotnet pack src/CursorAssist.Engine/CursorAssist.Engine.csproj -c Release -o ./nupkg
```

---

## プロジェクト構造

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

## ライセンス

[MIT](LICENSE)

---

<p align="center">
  Built by <a href="https://mcp-tool-shop.github.io/">MCP Tool Shop</a>
</p>
