<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.md">English</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/cursor-assist/readme.jpg" width="400" />
</p>

<p align="center">
  <a href="https://github.com/mcp-tool-shop-org/CursorAssist/actions/workflows/ci.yml"><img src="https://github.com/mcp-tool-shop-org/CursorAssist/actions/workflows/ci.yml/badge.svg" alt="CI" /></a>
  <a href="https://github.com/mcp-tool-shop-org/CursorAssist/blob/main/LICENSE"><img src="https://img.shields.io/github/license/mcp-tool-shop-org/CursorAssist" alt="License: MIT" /></a>
  <a href="https://mcp-tool-shop-org.github.io/CursorAssist/"><img src="https://img.shields.io/badge/Landing_Page-live-blue" alt="Landing Page" /></a>
</p>

**一个用于辅助光标控制、UI 可访问性基准测试和自适应运动技能训练的确定性引擎。**

---

## 为什么选择 CursorAssist？

- **真正有效的颤抖补偿。** 基于数字信号处理 (DSP) 的 EMA 平滑算法，采用自适应速度系数、频率感知截止频率、可选的双极滤波器（-40 dB/decade）和相位补偿——而不是一个简单的低通滤波器。
- **设计上的确定性。** 相同的输入始终产生相同的输出。 没有 `DateTime.Now`，没有 `Random`，没有平台相关的浮点数。 每一帧都可以通过 FNV-1a 哈希链进行重现和验证。
- **模块化的 NuGet 包。** 您可以选择仅使用模式（Canon）、仅使用跟踪格式（Trace）、仅使用策略映射器（Policy），或者使用完整的转换流水线（Engine）。 没有强制的依赖关系。
- **基于真实 DSP 算法的运动分析。** 采用幂律频率加权死区、基于颤抖频率的闭式 EMA 截止频率计算、通过余弦相干性检测方向意图，以及在每个激活边界上的迟滞现象。
- **214 多个测试用例。** 涵盖每个转换、每个策略规则和每个边界情况。

---

## 两个产品界面，一个核心引擎

这个工作空间包含两个产品，它们共享一个通用的确定性核心：

### CursorAssist -- 实时光标辅助

适用于患有运动障碍（颤抖、活动范围受限、疲劳）的人群。 作为一个系统托盘应用程序运行，实时拦截并转换原始指针输入。

- 通过自适应速度 EMA 平滑算法和相位校正进行颤抖补偿
- 自适应的软死区，采用二次压缩（没有硬边界）
- 具有迟滞特性的边缘阻力和目标磁性
- 带有版本信息的运动分析
- 确定性的策略映射：相同的运动分析始终产生相同的配置

### MouseTrainer -- 确定性的光标灵活性游戏

用于培养随着时间的推移减少辅助的需求。 这是一个使用 .NET MAUI 技术的桌面游戏，具有固定时间步长的模拟。

- 固定 60 Hz 模拟，具有可组合的蓝图修改器
- 通过 xorshift32 随机数生成器和 FNV-1a 哈希实现平台稳定的运行身份
- 拖放模式，用于真实世界的光标任务训练
- 基于事件驱动的音频提示系统，具有确定性的音量/音调抖动

---

## NuGet 包

CursorAssist 的四个库已发布到 NuGet，作为独立的包：

| 包 | NuGet | 描述 |
| --------- | ------- | ------------- |
| **CursorAssist.Canon** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Canon)](https://www.nuget.org/packages/CursorAssist.Canon) | 用于运动分析、辅助配置、难度计划和可访问性报告的、版本化的不可变模式和数据传输对象 (DTO)。 没有依赖项。 |
| **CursorAssist.Trace** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Trace)](https://www.nuget.org/packages/CursorAssist.Trace) | 用于光标输入记录和回放的 JSONL 跟踪格式 (`.castrace.jsonl`)。 线程安全的文件读写器。 没有依赖项。 |
| **CursorAssist.Policy** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Policy)](https://www.nuget.org/packages/CursorAssist.Policy) | 从运动分析到辅助配置的确定性映射器。 基于数字信号处理 (DSP) 的颤抖补偿，具有 EMA 截止频率公式、幂律死区和相位补偿。 依赖于 Canon。 |
| **CursorAssist.Engine** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Engine)](https://www.nuget.org/packages/CursorAssist.Engine) | 具有 60 Hz 累加器、可组合的 `IInputTransform` 链和指标收集的输入转换流水线。 依赖于 Canon 和 Trace。 |

### 安装

```bash
# Install individual packages
dotnet add package CursorAssist.Canon
dotnet add package CursorAssist.Trace
dotnet add package CursorAssist.Policy
dotnet add package CursorAssist.Engine
```

或者添加到您的 `.csproj` 文件中：

```xml
<PackageReference Include="CursorAssist.Canon" Version="1.0.0" />
<PackageReference Include="CursorAssist.Trace" Version="1.0.0" />
<PackageReference Include="CursorAssist.Policy" Version="1.0.0" />
<PackageReference Include="CursorAssist.Engine" Version="1.0.0" />
```

> 您只需要使用的包。 Canon 和 Trace 没有依赖项。 Policy 依赖于 Canon。 Engine 依赖于 Canon 和 Trace。

---

## 快速开始

### 将运动分析映射到辅助配置（Policy）

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

### 构建并运行转换流水线（Engine）

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

### 记录跟踪信息（Trace）

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

## 架构

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

### 转换流水线顺序

```
Raw Input --> SoftDeadzone --> Smoothing --> PhaseCompensation --> DirectionalIntent --> TargetMagnetism --> Output
```

每个转换模块都实现了 `IInputTransform` 接口，并通过 `TransformPipeline` 进行组合。`DeterministicPipeline` 将转换链封装在一个固定时间步长的累加循环中，并在每个时间步进行 FNV-1a 哈希验证。

---

## 设计原则

- **确定性是核心。** 相同的输入始终产生相同的输出。 不使用 `DateTime.Now`、`Random` 或平台相关的浮点数。 每一帧都经过哈希验证。
- **基于 DSP 理论，而非临时方案。** EMA（指数移动平均）的截止频率由封闭形式的公式推导得出（`fc = alpha * Fs / 2pi`）。 零区半径使用幂律频率加权。 相位补偿通过速度衰减来防止超调。
- **模块化，并强制执行边界。** 依赖关系是单向的，没有循环。 Canon 和 Trace 是叶节点。 应用程序是组合的根节点。
- **协议级别的身份验证。** ID 是永久且固定的。 使用 FNV-1a 哈希算法，并对参数进行规范化序列化。 使用 xorshift32 随机数生成器，以实现可重复的游戏会话。
- **可访问性是产品。** CursorAssist 的存在是为了让计算机能够被有运动障碍的人使用。 MouseTrainer 的存在是为了帮助人们提高操作技巧，从而减少对辅助的依赖。

---

## 从源代码构建

### 先决条件

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 或更高版本
- Windows 10/11（用于 Runtime.Windows 和 MouseTrainer.MauiHost）
- 任何操作系统（核心库，如 Canon、Trace、Policy 和 Engine，都是平台无关的）

### 构建

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

### 本地打包 NuGet 包

```bash
dotnet pack src/CursorAssist.Canon/CursorAssist.Canon.csproj -c Release -o ./nupkg
dotnet pack src/CursorAssist.Trace/CursorAssist.Trace.csproj -c Release -o ./nupkg
dotnet pack src/CursorAssist.Policy/CursorAssist.Policy.csproj -c Release -o ./nupkg
dotnet pack src/CursorAssist.Engine/CursorAssist.Engine.csproj -c Release -o ./nupkg
```

---

## 项目结构

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

## 许可证

[MIT](LICENSE)

---

<p align="center">
  Built by <a href="https://mcp-tool-shop.github.io/">MCP Tool Shop</a>
</p>
