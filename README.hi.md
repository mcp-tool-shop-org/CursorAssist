<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.md">English</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/cursor-assist/readme.jpg" width="400" />
</p>

<p align="center">
  <a href="https://github.com/mcp-tool-shop-org/CursorAssist/actions/workflows/ci.yml"><img src="https://github.com/mcp-tool-shop-org/CursorAssist/actions/workflows/ci.yml/badge.svg" alt="CI" /></a>
  <a href="https://github.com/mcp-tool-shop-org/CursorAssist/blob/main/LICENSE"><img src="https://img.shields.io/github/license/mcp-tool-shop-org/CursorAssist" alt="License: MIT" /></a>
  <a href="https://mcp-tool-shop-org.github.io/CursorAssist/"><img src="https://img.shields.io/badge/Landing_Page-live-blue" alt="Landing Page" /></a>
</p>

**सहायक कर्सर नियंत्रण, यूआई एक्सेसिबिलिटी बेंचमार्किंग और अनुकूल मोटर-कौशल प्रशिक्षण के लिए एक नियतात्मक इंजन।**

---

## CursorAssist क्यों?

- **कंपन (ट्रेमर) का वहन जो वास्तव में काम करता है।** डीएसपी-आधारित ईएमए स्मूथिंग, वेग-अनुकूलित अल्फा, आवृत्ति-जागरूक कटऑफ, वैकल्पिक ड्यूल-पोल (-40 डीबी/दशक) और चरण मुआवजा - यह कोई साधारण लो-पास फिल्टर नहीं है।
- **डिज़ाइन द्वारा नियतात्मक।** समान इनपुट हमेशा समान आउटपुट उत्पन्न करता है। कोई `DateTime.Now` नहीं, कोई `Random` नहीं, कोई प्लेटफ़ॉर्म-निर्भर फ्लोट नहीं। प्रत्येक फ्रेम को FNV-1a हैश श्रृंखलाओं के माध्यम से पुन: प्रस्तुत और सत्यापित किया जा सकता है।
- **मॉड्यूलर NuGet पैकेज।** केवल स्कीमा (Canon), केवल ट्रेस प्रारूप (Trace), केवल पॉलिसी मैपर (Policy) या संपूर्ण ट्रांसफॉर्म पाइपलाइन (Engine) का उपयोग करें। कोई बाध्यकारी नहीं।
- **वास्तविक डीएसपी गणित के साथ मोटर प्रोफाइलिंग।** पावर-लॉ आवृत्ति-भारित डेडज़ोन, कंपन आवृत्ति से बंद-रूप ईएमए कटऑफ, कोसाइन सुसंगतता के माध्यम से दिशात्मक इरादे का पता लगाना, और प्रत्येक एंगेजमेंट सीमा पर हिस्टेरेसिस।
- **214+ परीक्षण।** प्रत्येक ट्रांसफॉर्म, प्रत्येक पॉलिसी नियम, प्रत्येक सीमा स्थिति।

---

## दो उत्पाद, एक इंजन

यह कार्यक्षेत्र दो उत्पादों को शामिल करता है जो एक सामान्य नियतात्मक कोर साझा करते हैं:

### CursorAssist -- वास्तविक समय कर्सर सहायता

उन लोगों के लिए जिनके पास मोटर संबंधी कमियां हैं (कंपन, सीमित गति, थकान)। यह एक सिस्टम ट्रे एप्लिकेशन के रूप में चलता है जो वास्तविक समय में कच्चे पॉइंटर इनपुट को इंटरसेप्ट और ट्रांसफॉर्म करता है।

- वेग-अनुकूलित ईएमए स्मूथिंग और चरण सुधार के माध्यम से कंपन का वहन
- द्विघात संपीड़न (कोई कठोर किनारे नहीं) के साथ अनुकूली सॉफ्ट डेडज़ोन
- हिस्टेरेसिस के साथ एज प्रतिरोध और लक्ष्य चुंबकत्व
- संस्करणित स्कीमा के साथ मोटर प्रोफाइलिंग
- नियतात्मक पॉलिसी मैपिंग: समान प्रोफ़ाइल हमेशा समान कॉन्फ़िगरेशन उत्पन्न करती है

### MouseTrainer -- नियतात्मक कर्सर निपुणता खेल

समय के साथ कम सहायता की आवश्यकता होने के लिए निपुणता विकसित करने के लिए। एक .NET MAUI डेस्कटॉप गेम जिसमें निश्चित-टाइमस्टेप सिमुलेशन है।

- कंपोज़ेबल ब्लूप्रिंट म्यूटेटर के साथ 60 हर्ट्ज सिमुलेशन
- xorshift32 RNG और FNV-1a हैशिंग के माध्यम से प्लेटफ़ॉर्म-स्थिर रन पहचान
- वास्तविक दुनिया के कर्सर कार्य प्रशिक्षण के लिए ड्रैग-एंड-ड्रॉप गैंटलेट मोड
- नियतात्मक वॉल्यूम/पिच जिटर के साथ इवेंट-ड्रिवन ऑडियो क्यू सिस्टम

---

## NuGet पैकेज

CursorAssist की चार लाइब्रेरी को स्वतंत्र पैकेजों के रूप में NuGet पर प्रकाशित किया गया है:

| पैकेज | NuGet | विवरण |
| --------- | ------- | ------------- |
| **CursorAssist.Canon** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Canon)](https://www.nuget.org/packages/CursorAssist.Canon) | मोटर प्रोफाइल, सहायक कॉन्फ़िगरेशन, कठिनाई योजनाओं और एक्सेसिबिलिटी रिपोर्ट के लिए संस्करणित अपरिवर्तनीय स्कीमा और डीटीओ। शून्य निर्भरता। |
| **CursorAssist.Trace** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Trace)](https://www.nuget.org/packages/CursorAssist.Trace) | कर्सर इनपुट रिकॉर्डिंग और प्लेबैक के लिए JSONL ट्रेस प्रारूप (`.castrace.jsonl`)। थ्रेड-सेफ लेखक/पठक। शून्य निर्भरता। |
| **CursorAssist.Policy** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Policy)](https://www.nuget.org/packages/CursorAssist.Policy) | मोटर प्रोफाइल से सहायक कॉन्फ़िगरेशन तक नियतात्मक मैपर। ईएमए कटऑफ सूत्रों, पावर-लॉ डेडज़ोन और चरण मुआवजे के साथ डीएसपी-आधारित कंपन मुआवजा। Canon पर निर्भर करता है। |
| **CursorAssist.Engine** | [![NuGet](https://img.shields.io/nuget/v/CursorAssist.Engine)](https://www.nuget.org/packages/CursorAssist.Engine) | 60 हर्ट्ज एक्यूमुलेटर, कंपोज़ेबल `IInputTransform` श्रृंखला और मेट्रिक्स संग्रह के साथ इनपुट ट्रांसफॉर्म पाइपलाइन। Canon और Trace पर निर्भर करता है। |

### इंस्टॉल करें

```bash
# Install individual packages
dotnet add package CursorAssist.Canon
dotnet add package CursorAssist.Trace
dotnet add package CursorAssist.Policy
dotnet add package CursorAssist.Engine
```

या अपने `.csproj` में जोड़ें:

```xml
<PackageReference Include="CursorAssist.Canon" Version="1.0.0" />
<PackageReference Include="CursorAssist.Trace" Version="1.0.0" />
<PackageReference Include="CursorAssist.Policy" Version="1.0.0" />
<PackageReference Include="CursorAssist.Engine" Version="1.0.0" />
```

> आपको केवल उन पैकेजों की आवश्यकता है जिनका आप उपयोग करते हैं। Canon और Trace शून्य-निर्भरता वाले हैं। Policy, Canon पर निर्भर करता है। Engine, Canon और Trace पर निर्भर करता है।

---

## शुरुआत कैसे करें

### एक मोटर प्रोफ़ाइल को एक सहायक कॉन्फ़िगरेशन पर मैप करें (Policy)

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

### ट्रांसफॉर्म पाइपलाइन बनाएं और चलाएं (Engine)

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

### एक ट्रेस रिकॉर्ड करें (Trace)

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

## आर्किटेक्चर

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

### ट्रांसफॉर्म पाइपलाइन क्रम

```
Raw Input --> SoftDeadzone --> Smoothing --> PhaseCompensation --> DirectionalIntent --> TargetMagnetism --> Output
```

प्रत्येक रूपांतरण `IInputTransform` इंटरफ़ेस को लागू करता है और `TransformPipeline` के माध्यम से बनाया जाता है। `DeterministicPipeline` एक निश्चित समय अंतराल वाले लूप में इन रूपांतरणों को लपेटता है, और प्रत्येक चक्र में FNV-1a हैश सत्यापन किया जाता है।

---

## डिजाइन सिद्धांत

- **नियतिवाद मौलिक है।** समान इनपुट हमेशा समान आउटपुट उत्पन्न करता है। कोई `DateTime.Now` नहीं, कोई `Random` नहीं, और "हॉट पाथ" में कोई प्लेटफ़ॉर्म-निर्भर फ़्लोट नहीं। प्रत्येक फ़्रेम का हैश सत्यापित किया जाता है।
- **डीएसपी (डिजिटल सिग्नल प्रोसेसिंग) पर आधारित, मनमाने ढंग से नहीं।** ईएमए (एक्सपोनेंशियल मूविंग एवरेज) कटऑफ आवृत्तियाँ बंद-रूप सूत्रों से प्राप्त की जाती हैं (`fc = alpha * Fs / 2pi`)। डेडज़ोन त्रिज्याएँ पावर-लॉ आवृत्ति भार का उपयोग करती हैं। चरण क्षतिपूर्ति वेग-क्षीण होती है ताकि ओवरशूट को रोका जा सके।
- **मॉड्यूलर, लेकिन सीमाओं के साथ।** एक-तरफ़ा निर्भरताएँ, कोई चक्र नहीं। कैनन और ट्रेस अंतिम घटक हैं। एप्लिकेशन रचना के मूल हैं।
- **प्रोटोकॉल-ग्रेड पहचान।** आईडी स्थायी और अपरिवर्तनीय हैं। कैननिकल पैरामीटर सीरियललाइजेशन के साथ FNV-1a हैशिंग। पुनरुत्पादित गेम सत्रों के लिए xorshift32 RNG।
- **पहुंच योग्यता ही उत्पाद है।** CursorAssist का उद्देश्य उन लोगों के लिए कंप्यूटर को उपयोग करने योग्य बनाना है जिन्हें मोटर संबंधी अक्षमता है। MouseTrainer का उद्देश्य लोगों को ऐसी कौशल विकसित करने में मदद करना है जिससे उन्हें समय के साथ कम सहायता की आवश्यकता हो।

---

## स्रोत कोड से निर्माण

### आवश्यकताएं

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) या बाद का संस्करण
- विंडोज 10/11 (Runtime.Windows और MouseTrainer.MauiHost के लिए)
- मुख्य पुस्तकालयों के लिए कोई भी ऑपरेटिंग सिस्टम (Canon, Trace, Policy, Engine प्लेटफ़ॉर्म-स्वतंत्र हैं)

### निर्माण

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

### NuGet पैकेज स्थानीय रूप से बनाएं

```bash
dotnet pack src/CursorAssist.Canon/CursorAssist.Canon.csproj -c Release -o ./nupkg
dotnet pack src/CursorAssist.Trace/CursorAssist.Trace.csproj -c Release -o ./nupkg
dotnet pack src/CursorAssist.Policy/CursorAssist.Policy.csproj -c Release -o ./nupkg
dotnet pack src/CursorAssist.Engine/CursorAssist.Engine.csproj -c Release -o ./nupkg
```

---

## परियोजना संरचना

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

## लाइसेंस

[MIT](LICENSE)

---

<p align="center">
  Built by <a href="https://mcp-tool-shop.github.io/">MCP Tool Shop</a>
</p>
