using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using CursorAssist.Canon.Schemas;
using CursorAssist.Engine.Core;
using CursorAssist.Engine.Mapping;
using CursorAssist.Engine.Transforms;
using CursorAssist.Runtime.Core;
using CursorAssist.Runtime.Windows;
using CursorAssist.Runtime.Windows.Interop;
using CursorAssist.Trace;

namespace CursorAssist.Pilot;

/// <summary>
/// Dev-only console pilot host. Orchestrates the full runtime stack:
///   MouseCapture → EngineThread → MouseInjector
/// with telemetry logging, config management, and kill switch.
///
/// Usage:
///   cursorassist-pilot [options]
///     --config &lt;assist.json&gt;    Load AssistiveConfig from file
///     --profile &lt;motor.json&gt;    Load MotorProfile, derive config via mapper
///     --trace                     Enable tick-level trace logging
///     --session-dir &lt;path&gt;       Output directory (default: ./sessions)
///     --export-config &lt;path&gt;     Export active config to JSON file and exit
///     --safe-default &lt;level&gt;     Use built-in safe default (minimal|moderate)
///     -h, --help                  Show help
/// </summary>
public static partial class Program
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static volatile bool _running = true;
    private static volatile bool _emergencyStopFired;
    private static uint _mainThreadId;

    [LibraryImport("kernel32.dll")]
    private static partial uint GetCurrentThreadId();

    public static int Main(string[] args)
    {
        // Capture main thread ID immediately (needed for PostThreadMessage from other threads)
        _mainThreadId = GetCurrentThreadId();

        if (args.Length == 0 || HasFlag(args, "-h") || HasFlag(args, "--help"))
        {
            PrintUsage();
            return 0;
        }

        // ── Parse args ────────────────────────────────────────────
        string? configPath = GetArg(args, "--config");
        string? profilePath = GetArg(args, "--profile");
        string? safeDefault = GetArg(args, "--safe-default");
        string? exportPath = GetArg(args, "--export-config");
        string sessionDir = GetArg(args, "--session-dir") ?? "./sessions";
        bool traceEnabled = HasFlag(args, "--trace");

        // ── Resolve config ────────────────────────────────────────
        AssistiveConfig? config = null;
        MotorProfile? profile = null;

        if (configPath is not null)
        {
            if (!File.Exists(configPath))
            {
                Console.Error.WriteLine($"Config file not found: {configPath}");
                return 1;
            }
            config = JsonSerializer.Deserialize<AssistiveConfig>(
                File.ReadAllText(configPath), JsonOpts);
        }
        else if (profilePath is not null)
        {
            if (!File.Exists(profilePath))
            {
                Console.Error.WriteLine($"Profile file not found: {profilePath}");
                return 1;
            }
            profile = JsonSerializer.Deserialize<MotorProfile>(
                File.ReadAllText(profilePath), JsonOpts);
            if (profile is not null)
                config = ProfileToConfigMapper.Map(profile);
        }
        else if (safeDefault is not null)
        {
            config = safeDefault.ToLowerInvariant() switch
            {
                "minimal" => SafeDefaults.Minimal(),
                "moderate" => SafeDefaults.Moderate(),
                _ => null
            };
            if (config is null)
            {
                Console.Error.WriteLine($"Unknown safe default level: {safeDefault}. Use 'minimal' or 'moderate'.");
                return 1;
            }
        }

        if (config is null)
        {
            Console.Error.WriteLine("No config source specified. Use --config, --profile, or --safe-default.");
            PrintUsage();
            return 1;
        }

        // ── Export config and exit ────────────────────────────────
        if (exportPath is not null)
        {
            string json = JsonSerializer.Serialize(config, JsonOpts);
            File.WriteAllText(exportPath, json);
            Console.WriteLine($"Config exported to {exportPath}");
            return 0;
        }

        // ── Apply runtime safety clamp ────────────────────────────
        config = EngineThread.ClampConfig(config);

        // ── Build transform pipeline (all 5 transforms) ──────────
        var pipeline = new TransformPipeline()
            .Add(new SoftDeadzoneTransform())
            .Add(new SmoothingTransform())
            .Add(new PhaseCompensationTransform())
            .Add(new DirectionalIntentTransform())
            .Add(new TargetMagnetismTransform());

        // ── Create runtime components ─────────────────────────────
        var engine = new EngineThread(pipeline);
        using var capture = new MouseCapture(engine);
        using var injector = new MouseInjector(engine);
        using var killSwitch = new HotkeyKillSwitch();
        using var telemetry = new TelemetryWriter();

        // ── Wire kill switch ──────────────────────────────────────
        killSwitch.Triggered += () =>
        {
            _emergencyStopFired = true;
            engine.EmergencyStop();
            _running = false;
            NativeMethods.PostThreadMessage(_mainThreadId, NativeMethods.WM_QUIT, 0, 0);
        };

        // ── Wire Ctrl+C ──────────────────────────────────────────
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true; // Prevent immediate process termination
            _running = false;
            NativeMethods.PostThreadMessage(_mainThreadId, NativeMethods.WM_QUIT, 0, 0);
        };

        // ── Begin telemetry session ───────────────────────────────
        var sessionStarted = DateTimeOffset.UtcNow;
        string sessionId = telemetry.Begin(sessionDir, config);

        TraceWriter? traceWriter = null;
        if (traceEnabled)
        {
            var header = new TraceHeader
            {
                SourceApp = "cursorassist-pilot",
                SourceVersion = "0.1.0",
                FixedHz = 60,
                RunId = sessionId
            };
            traceWriter = telemetry.AttachTraceWriter(header);
        }

        // ── Get current cursor position ───────────────────────────
        float initX = 960f, initY = 540f;
        if (NativeMethods.GetCursorPos(out var pt))
        {
            initX = pt.X;
            initY = pt.Y;
        }

        // ── Print session info ────────────────────────────────────
        Console.WriteLine("CursorAssist Pilot v0.1");
        Console.WriteLine(new string('\u2500', 56));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Session:      {sessionId}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Config:       {config.SourceProfileId}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Smoothing:    strength={config.SmoothingStrength:F2}  minAlpha={config.SmoothingMinAlpha:F2}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Deadzone:     {config.DeadzoneRadiusVpx:F2} vpx"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Phase comp:   {config.PhaseCompensationGainS * 1000f:F1} ms"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Trace:        {(traceEnabled ? "enabled" : "disabled")}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Cursor init:  ({initX:F0}, {initY:F0})"));
        Console.WriteLine(new string('\u2500', 56));
        Console.WriteLine();
        Console.WriteLine("Assist ACTIVE. Press Ctrl+C to stop.");
        Console.WriteLine("Emergency stop: Ctrl+Shift+Pause");
        Console.WriteLine();

        // ── Start runtime ─────────────────────────────────────────
        engine.Enable(initX, initY, config);
        if (profile is not null)
            engine.UpdateProfile(profile);

        injector.Start();
        killSwitch.Arm();

        // ── Start mouse capture + message pump on main thread ─────
        // WH_MOUSE_LL requires a message pump on the calling thread.
        // We install the hook, then pump messages until WM_QUIT.
        capture.Start();

        // Message pump — blocks until WM_QUIT or _running becomes false
        while (_running && NativeMethods.GetMessageW(out var msg, 0, 0, 0))
        {
            NativeMethods.TranslateMessage(in msg);
            NativeMethods.DispatchMessageW(in msg);
        }

        // ── Shutdown ──────────────────────────────────────────────
        Console.WriteLine();
        Console.WriteLine("Stopping...");

        capture.Stop();
        injector.Stop();
        engine.Disable();
        killSwitch.Disarm();

        // ── Write session summary ─────────────────────────────────
        var sessionEnded = DateTimeOffset.UtcNow;
        float durationS = (float)(sessionEnded - sessionStarted).TotalSeconds;
        string exitReason = _emergencyStopFired ? "kill-switch" : "user";

        var summary = new SessionSummary
        {
            SessionId = sessionId,
            StartedUtc = sessionStarted,
            EndedUtc = sessionEnded,
            DurationSeconds = durationS,
            ConfigHash = TelemetryWriter.ComputeConfigHash(config),
            FixedHz = 60,
            TotalTicks = 0, // Placeholder — real tick count from TracingMetricsSink (Commit 3.5)
            OverrunCount = engine.OverrunCount,
            EmergencyStopFired = _emergencyStopFired,
            ExitReason = exitReason
        };

        telemetry.End(summary);

        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Session ended. Duration: {durationS:F1}s, Exit: {exitReason}"));
        if (telemetry.SessionDir is not null)
            Console.WriteLine($"Output: {telemetry.SessionDir}");

        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("CursorAssist Pilot v0.1 — Dev-only console host");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  cursorassist-pilot [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --config <assist.json>    Load AssistiveConfig from file");
        Console.WriteLine("  --profile <motor.json>    Load MotorProfile, derive config via mapper");
        Console.WriteLine("  --safe-default <level>    Use built-in safe default (minimal|moderate)");
        Console.WriteLine("  --trace                   Enable tick-level trace logging");
        Console.WriteLine("  --session-dir <path>      Output directory (default: ./sessions)");
        Console.WriteLine("  --export-config <path>    Export active config to JSON file and exit");
        Console.WriteLine("  -h, --help                Show help");
        Console.WriteLine();
        Console.WriteLine("Config priority: --config > --profile > --safe-default");
        Console.WriteLine();
        Console.WriteLine("Controls:");
        Console.WriteLine("  Ctrl+C                    Graceful stop");
        Console.WriteLine("  Ctrl+Shift+Pause          Emergency stop (kill switch)");
    }

    private static string? GetArg(string[] args, string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == flag) return args[i + 1];
        return null;
    }

    private static bool HasFlag(string[] args, string flag)
        => Array.Exists(args, a => a == flag);
}
