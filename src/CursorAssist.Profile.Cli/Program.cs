using System.Text.Json;
using CursorAssist.Engine.Core;
using CursorAssist.Engine.Metrics;
using CursorAssist.Trace;

namespace CursorAssist.Profile.Cli;

/// <summary>
/// Profile CLI: ingest a .castrace.jsonl trace, run MotorProfileSink,
/// emit MotorProfile.v1.json and optionally AssistiveConfig.v1.json.
///
/// Usage:
///   cursorassist-profile ingest trace.castrace.jsonl --out motor.json [--emit-config assist.json]
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
        if (args.Length < 2 || args[0] != "ingest")
        {
            PrintUsage();
            return args.Length > 0 && args[0] is "-h" or "--help" ? 0 : 1;
        }

        string tracePath = args[1];
        string? outPath = GetArg(args, "--out");
        string? configPath = GetArg(args, "--emit-config");
        string profileId = GetArg(args, "--profile-id") ?? Path.GetFileNameWithoutExtension(tracePath);

        if (!File.Exists(tracePath))
        {
            Console.Error.WriteLine($"Trace file not found: {tracePath}");
            return 1;
        }

        Console.WriteLine($"Ingesting: {tracePath}");

        using var reader = new TraceReader(tracePath);
        var header = reader.ReadHeader();

        Console.WriteLine($"  Source: {header.SourceApp} v{header.SourceVersion ?? "?"}");
        Console.WriteLine($"  FixedHz: {header.FixedHz}");
        Console.WriteLine($"  Seed: {(header.RunSeed.HasValue ? $"0x{header.RunSeed.Value:X8}" : "n/a")}");

        // Feed samples through MotorProfileSink
        var sink = new MotorProfileSink();
        var pipeline = new TransformPipeline();
        var engine = new DeterministicPipeline(pipeline);

        float prevX = 0f, prevY = 0f;
        bool hasPrev = false;
        int sampleCount = 0;

        // Simple heuristic: treat each significant pause as a trial boundary
        // For v0, treat the entire trace as one trial per "movement burst"
        bool inBurst = false;
        var dummyTarget = new TargetInfo("trace", header.VirtualWidth / 2f, header.VirtualHeight / 2f, 60f, 40f);

        foreach (var sample in reader.ReadSamples())
        {
            float dx = hasPrev ? sample.X - prevX : 0f;
            float dy = hasPrev ? sample.Y - prevY : 0f;
            float speed = MathF.Sqrt(dx * dx + dy * dy);

            var input = new InputSample(sample.X, sample.Y, dx, dy,
                (sample.Buttons & 0x01) != 0,
                (sample.Buttons & 0x02) != 0,
                sample.Tick);

            var ctx = new TransformContext
            {
                Tick = sample.Tick,
                Dt = 1f / header.FixedHz,
                Targets = [dummyTarget]
            };

            // Burst detection: if cursor is moving, we're in a burst
            if (speed > 0.5f && !inBurst)
            {
                inBurst = true;
                sink.BeginTrial(sample.X, sample.Y);
            }
            else if (speed < 0.1f && inBurst && (sample.Buttons & 0x01) != 0)
            {
                // Click at low speed = end of trial
                sink.EndTrial(sample.Tick, dummyTarget);
                inBurst = false;
            }

            sink.RecordTick(sample.Tick, in input, in input, [dummyTarget]);

            prevX = sample.X;
            prevY = sample.Y;
            hasPrev = true;
            sampleCount++;
        }

        // End any open trial
        if (inBurst)
            sink.EndTrial(sampleCount, dummyTarget);

        Console.WriteLine($"  Samples: {sampleCount}");

        var profile = sink.ExportProfile(profileId);

        Console.WriteLine($"\n  Motor Profile:");
        Console.WriteLine($"    Tremor freq:    {profile.TremorFrequencyHz:F1} Hz");
        Console.WriteLine($"    Tremor amp:     {profile.TremorAmplitudeVpx:F2} vpx");
        Console.WriteLine($"    Path eff:       {profile.PathEfficiency:F3}");
        Console.WriteLine($"    Overshoot rate: {profile.OvershootRate:F2}");
        Console.WriteLine($"    Mean TTT:       {profile.MeanTimeToTargetS:F3}s");
        Console.WriteLine($"    Click stab:     {profile.ClickStabilityVpx:F2} vpx");
        Console.WriteLine($"    Samples:        {profile.SampleCount}");

        // Write profile
        string profileJson = JsonSerializer.Serialize(profile, JsonOpts);
        if (outPath is not null)
        {
            File.WriteAllText(outPath, profileJson);
            Console.WriteLine($"\n  Profile written to: {outPath}");
        }
        else
        {
            Console.WriteLine($"\n{profileJson}");
        }

        // Optionally emit config
        if (configPath is not null)
        {
            var config = Policy.ProfileToConfigMapper.Map(profile);
            File.WriteAllText(configPath, JsonSerializer.Serialize(config, JsonOpts));
            Console.WriteLine($"  Config written to: {configPath}");
        }

        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("CursorAssist Profile CLI");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  cursorassist-profile ingest <trace.castrace.jsonl> [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --out <motor.json>          Output motor profile path");
        Console.WriteLine("  --emit-config <assist.json> Also emit assistive config");
        Console.WriteLine("  --profile-id <id>           Override profile ID");
        Console.WriteLine("  -h, --help                  Show this help");
    }

    private static string? GetArg(string[] args, string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == flag) return args[i + 1];
        return null;
    }
}
