using System.Diagnostics;
using System.Text.Json;
using CursorAssist.Canon.Schemas;
using CursorAssist.Engine.Core;
using CursorAssist.Engine.Layout;
using CursorAssist.Policy;
using CursorAssist.Engine.Metrics;
using CursorAssist.Engine.Transforms;

namespace CursorAssist.Benchmark.Cli;

/// <summary>
/// CLI benchmark tool. Runs deterministic trials against a layout,
/// computes Fitts' Law metrics, and outputs a UIAccessibilityReport.
///
/// Usage:
///   cursorassist-bench layout.json [--profile motor.json] [--assist assist.json] [--trials 50] [--seed 42] [-o report.json]
/// </summary>
public static class Program
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintUsage();
            return 0;
        }

        string layoutPath = args[0];
        string? profilePath = GetArg(args, "--profile");
        string? assistPath = GetArg(args, "--assist");
        int trials = int.TryParse(GetArg(args, "--trials"), out var t) ? t : 50;
        uint seed = uint.TryParse(GetArg(args, "--seed"), out var s) ? s : 0xC0FFEEu;
        string? outputPath = GetArg(args, "-o") ?? GetArg(args, "--output");

        // Load layout
        if (!File.Exists(layoutPath))
        {
            Console.Error.WriteLine($"Layout file not found: {layoutPath}");
            return 1;
        }
        var layout = JsonSerializer.Deserialize<UILayout>(File.ReadAllText(layoutPath), JsonOpts);
        if (layout is null || layout.Targets.Count == 0)
        {
            Console.Error.WriteLine("Layout is empty or invalid.");
            return 1;
        }

        // Load optional profile
        MotorProfile? profile = null;
        if (profilePath is not null)
        {
            if (!File.Exists(profilePath))
            {
                Console.Error.WriteLine($"Profile file not found: {profilePath}");
                return 1;
            }
            profile = JsonSerializer.Deserialize<MotorProfile>(File.ReadAllText(profilePath), JsonOpts);
        }

        // Load or derive assist config
        AssistiveConfig? assistConfig = null;
        if (assistPath is not null)
        {
            if (!File.Exists(assistPath))
            {
                Console.Error.WriteLine($"Assist config not found: {assistPath}");
                return 1;
            }
            assistConfig = JsonSerializer.Deserialize<AssistiveConfig>(File.ReadAllText(assistPath), JsonOpts);
        }
        else if (profile is not null)
        {
            assistConfig = ProfileToConfigMapper.Map(profile);
        }

        Console.WriteLine($"Layout: {layout.LayoutId} ({layout.Targets.Count} targets)");
        Console.WriteLine($"Trials: {trials}, Seed: 0x{seed:X8}");
        Console.WriteLine($"Assist: {(assistConfig is not null ? "enabled" : "baseline")}");
        Console.WriteLine();

        // Run baseline
        Console.WriteLine("=== Baseline (no assist) ===");
        var baselineReport = RunTrials(layout, trials, seed, config: null, profile);
        PrintSummary(baselineReport);

        // Run assisted (if config available)
        UIAccessibilityReport? assistedReport = null;
        if (assistConfig is not null)
        {
            Console.WriteLine();
            Console.WriteLine("=== Assisted ===");
            assistedReport = RunTrials(layout, trials, seed, assistConfig, profile);
            PrintSummary(assistedReport);

            // Comparison
            Console.WriteLine();
            Console.WriteLine("=== Improvement ===");
            PrintComparison(baselineReport, assistedReport);
        }

        // Write output
        var reportToWrite = assistedReport ?? baselineReport;
        if (outputPath is not null)
        {
            File.WriteAllText(outputPath, JsonSerializer.Serialize(reportToWrite, JsonOpts));
            Console.WriteLine($"\nReport written to {outputPath}");
        }

        return 0;
    }

    private static UIAccessibilityReport RunTrials(
        UILayout layout, int trialCount, uint seed,
        AssistiveConfig? config, MotorProfile? profile)
    {
        var pipeline = new TransformPipeline();
        if (config is not null)
        {
            pipeline.Add(new TargetMagnetismTransform());
        }

        var sink = new BenchmarkMetricsSink();
        // Don't pass sink to engine — engine.Reset() would clear it between trials.
        // Feed sink manually per tick instead.
        var engine = new DeterministicPipeline(pipeline);

        var targets = layout.Targets.Select(t => t.ToTargetInfo()).ToArray();
        var rng = new SimpleRng(seed);

        for (int trial = 0; trial < trialCount; trial++)
        {
            engine.Reset();
            pipeline.Reset();

            // Pick a random start and target
            int targetIdx = (int)(rng.Next() % (uint)targets.Length);
            var target = targets[targetIdx];

            float startX = rng.NextFloat() * layout.Width;
            float startY = rng.NextFloat() * layout.Height;

            sink.BeginTrial(target, startX, startY);

            // Simulate a cursor path: lerp with noise toward target
            float curX = startX, curY = startY;
            int maxTicks = 600; // 10 seconds at 60Hz cap

            for (int tick = 0; tick < maxTicks; tick++)
            {
                // Move toward target with some noise
                float dirX = target.CenterX - curX;
                float dirY = target.CenterY - curY;
                float dist = MathF.Sqrt(dirX * dirX + dirY * dirY);

                if (dist < 2f)
                {
                    // Close enough — click
                    var clickSample = new InputSample(curX, curY, 0, 0, true, false, tick);
                    var ctx = new TransformContext
                    {
                        Tick = tick,
                        Dt = 1f / 60f,
                        Targets = [target],
                        Config = config,
                        Profile = profile
                    };
                    var clickResult = engine.FixedStep(in clickSample, ctx);
                    sink.RecordTick(tick, in clickSample, clickResult.FinalCursor, [target]);
                    sink.EndTrial(true, tick);
                    break;
                }

                // Speed: 3-8 vpx/tick with noise
                float speed = 3f + rng.NextFloat() * 5f;
                float nx = (dirX / dist) * speed + (rng.NextFloat() - 0.5f) * 4f;
                float ny = (dirY / dist) * speed + (rng.NextFloat() - 0.5f) * 4f;
                curX += nx;
                curY += ny;

                var sample = new InputSample(curX, curY, nx, ny, false, false, tick);
                var context = new TransformContext
                {
                    Tick = tick,
                    Dt = 1f / 60f,
                    Targets = [target],
                    Config = config,
                    Profile = profile
                };
                var result = engine.FixedStep(in sample, context);
                sink.RecordTick(tick, in sample, result.FinalCursor, [target]);

                // Feed assisted cursor position back so the next tick simulates
                // realistic movement from where the engine actually placed the cursor.
                curX = result.FinalCursor.X;
                curY = result.FinalCursor.Y;

                if (tick == maxTicks - 1)
                    sink.EndTrial(false, tick);
            }
        }

        var summary = sink.ComputeSummary();

        return new UIAccessibilityReport
        {
            GeneratedUtc = DateTimeOffset.UtcNow,
            LayoutId = layout.LayoutId,
            ProfileId = profile?.ProfileId,
            Assisted = config is not null,
            TrialCount = summary.TrialCount,
            MeanEffectiveWidthVpx = summary.MeanEffectiveWidthVpx,
            MeanIndexOfDifficulty = summary.MeanIndexOfDifficulty,
            ErrorRate = summary.ErrorRate,
            MeanTimeToTargetS = summary.MeanTimeToTargetS,
            MeanPathEfficiency = summary.MeanPathEfficiency,
            ThroughputBitsPerS = summary.ThroughputBitsPerS
        };
    }

    private static void PrintSummary(UIAccessibilityReport report)
    {
        Console.WriteLine($"  Trials:           {report.TrialCount}");
        Console.WriteLine($"  Error rate:       {report.ErrorRate:P1}");
        Console.WriteLine($"  Mean time:        {report.MeanTimeToTargetS:F3}s");
        Console.WriteLine($"  Path efficiency:  {report.MeanPathEfficiency:F3}");
        Console.WriteLine($"  Index of diff:    {report.MeanIndexOfDifficulty:F2} bits");
        Console.WriteLine($"  Effective width:  {report.MeanEffectiveWidthVpx:F1} vpx");
        Console.WriteLine($"  Throughput:       {report.ThroughputBitsPerS:F2} bits/s");
    }

    private static void PrintComparison(UIAccessibilityReport baseline, UIAccessibilityReport assisted)
    {
        float timeDelta = baseline.MeanTimeToTargetS > 0
            ? (baseline.MeanTimeToTargetS - assisted.MeanTimeToTargetS) / baseline.MeanTimeToTargetS
            : 0f;
        float pathDelta = assisted.MeanPathEfficiency - baseline.MeanPathEfficiency;
        float tpDelta = baseline.ThroughputBitsPerS > 0
            ? (assisted.ThroughputBitsPerS - baseline.ThroughputBitsPerS) / baseline.ThroughputBitsPerS
            : 0f;

        Console.WriteLine($"  Time improvement:       {timeDelta:P1}");
        Console.WriteLine($"  Path eff improvement:   {pathDelta:+0.000;-0.000}");
        Console.WriteLine($"  Throughput improvement:  {tpDelta:P1}");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("CursorAssist Benchmark CLI");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  cursorassist-bench <layout.json> [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --profile <motor.json>   Motor profile for assisted trials");
        Console.WriteLine("  --assist <assist.json>    Assistive config (overrides profile mapping)");
        Console.WriteLine("  --trials <n>              Number of trials per condition (default: 50)");
        Console.WriteLine("  --seed <n>                RNG seed (default: 0xC0FFEE)");
        Console.WriteLine("  -o, --output <path>       Output report path (JSON)");
        Console.WriteLine("  -h, --help                Show this help");
    }

    private static string? GetArg(string[] args, string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == flag) return args[i + 1];
        return null;
    }
}

/// <summary>
/// Simple deterministic RNG for benchmark simulation (separate from engine state).
/// </summary>
internal struct SimpleRng
{
    private uint _state;

    public SimpleRng(uint seed) { _state = seed == 0 ? 0xDEADBEEFu : seed; }

    public uint Next()
    {
        uint x = _state;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _state = x;
        return x;
    }

    public float NextFloat() => (Next() & 0x00FFFFFF) / 16777216f;
}
