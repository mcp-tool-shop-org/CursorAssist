using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using CursorAssist.Canon.Schemas;
using CursorAssist.Engine.Core;
using CursorAssist.Engine.Analysis;
using CursorAssist.Engine.Mapping;
using CursorAssist.Engine.Metrics;
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
///     --precision                  Enable dual-pole precision mode
///     --targets                    Enable UI Automation target awareness
///     --calibrate                  Run 5-second calibration session
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
        bool precisionMode = HasFlag(args, "--precision");
        bool targetsEnabled = HasFlag(args, "--targets");
        bool calibrate = HasFlag(args, "--calibrate");

        // ── Calibration mode (standalone) ───────────────────────────
        if (calibrate)
            return RunCalibration(exportPath);

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

        // ── Apply precision mode override ──────────────────────────
        if (precisionMode)
            config = config with { PrecisionModeEnabled = true };

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

        // ── Begin telemetry session (before engine, so trace writer exists) ──
        using var telemetry = new TelemetryWriter();
        var sessionStarted = DateTimeOffset.UtcNow;
        string sessionId = telemetry.Begin(sessionDir, config);

        // ── Create trace writer + metrics sink (opt-in) ──────────
        TracingMetricsSink? metricsSink = null;
        if (traceEnabled)
        {
            var header = new TraceHeader
            {
                SourceApp = "cursorassist-pilot",
                SourceVersion = "0.1.0",
                FixedHz = 60,
                RunId = sessionId
            };
            var traceWriter = telemetry.AttachTraceWriter(header);
            if (traceWriter is not null)
                metricsSink = new TracingMetricsSink(traceWriter);
        }

        // ── Create target provider (opt-in) ─────────────────────────
        UIAutomationTargetProvider? targetProvider = null;
        if (targetsEnabled)
            targetProvider = new UIAutomationTargetProvider();

        // ── Create runtime components ─────────────────────────────
        var engine = new EngineThread(pipeline, metrics: metricsSink, targetProvider: targetProvider);
        using var capture = new MouseCapture(engine);
        using var injector = new MouseInjector(engine);
        using var killSwitch = new HotkeyKillSwitch();

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
            $"Precision:    {(config.PrecisionModeEnabled ? "enabled" : "disabled")}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Targets:      {(targetsEnabled ? "UI Automation" : "disabled")}"));
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

        targetProvider?.Start();
        injector.Start();
        killSwitch.Arm();

        // ── Start live dashboard ──────────────────────────────────
        using var dashboard = new ConsoleDashboard(
            engine, config, sessionId, sessionStarted, killSwitch, targetProvider);
        dashboard.Start();

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
        dashboard.Stop();

        Console.WriteLine();
        Console.WriteLine("Stopping...");

        capture.Stop();
        injector.Stop();
        targetProvider?.Stop();
        targetProvider?.Dispose();
        engine.Disable();
        killSwitch.Disarm();

        // ── Write session summary ─────────────────────────────────
        var sessionEnded = DateTimeOffset.UtcNow;
        float durationS = (float)(sessionEnded - sessionStarted).TotalSeconds;
        string exitReason = _emergencyStopFired ? "kill-switch" : "user";

        // Extract stats from metrics sink (if active)
        long totalTicks = 0;
        float meanVelocity = 0f;
        float peakVelocity = 0f;
        if (metricsSink is not null)
        {
            var stats = metricsSink.ExportStats();
            totalTicks = stats.TotalTicks;
            meanVelocity = stats.MeanVelocity;
            peakVelocity = stats.PeakVelocity;
        }
        else
        {
            // Estimate tick count from duration when trace is disabled
            totalTicks = (long)(durationS * 60f);
        }

        var summary = new SessionSummary
        {
            SessionId = sessionId,
            StartedUtc = sessionStarted,
            EndedUtc = sessionEnded,
            DurationSeconds = durationS,
            ConfigHash = TelemetryWriter.ComputeConfigHash(config),
            FixedHz = 60,
            TotalTicks = totalTicks,
            OverrunCount = engine.OverrunCount,
            MeanVelocity = meanVelocity,
            PeakVelocity = peakVelocity,
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

    private static int RunCalibration(string? exportPath)
    {
        const int durationTicks = 300; // 5 seconds at 60 Hz
        var session = new CalibrationSession(durationTicks);

        Console.WriteLine("CursorAssist Calibration");
        Console.WriteLine(new string('\u2500', 56));
        Console.WriteLine("Move your mouse naturally for 5 seconds.");
        Console.WriteLine("Include both intentional movements and pauses.");
        Console.WriteLine();
        Console.Write("Ready? Press Enter to start...");
        Console.ReadLine();
        Console.WriteLine();
        Console.WriteLine("Recording... move your mouse now!");

        // Use a simple polling loop with WH_MOUSE_LL-captured deltas
        // For simplicity in the pilot, we use raw input from the capture system
        // For now: use a simulated timer-based approach that reads cursor position deltas
        float prevX = 0f, prevY = 0f;
        if (NativeMethods.GetCursorPos(out var startPt))
        {
            prevX = startPt.X;
            prevY = startPt.Y;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        double tickInterval = 1.0 / 60.0;
        double nextTick = tickInterval;

        while (!session.IsComplete)
        {
            double elapsed = sw.Elapsed.TotalSeconds;
            if (elapsed >= nextTick)
            {
                if (NativeMethods.GetCursorPos(out var pt))
                {
                    float dx = pt.X - prevX;
                    float dy = pt.Y - prevY;
                    session.RecordTick(dx, dy);
                    prevX = pt.X;
                    prevY = pt.Y;
                }
                else
                {
                    session.RecordTick(0f, 0f);
                }
                nextTick += tickInterval;

                // Progress indicator
                int pct = session.TickCount * 100 / durationTicks;
                Console.Write($"\rProgress: {pct}%  ");
            }

            Thread.Sleep(1);
        }

        Console.WriteLine();
        Console.WriteLine();

        var result = session.GetResult();
        var c = CultureInfo.InvariantCulture;

        Console.WriteLine("Calibration Results:");
        Console.WriteLine(new string('\u2500', 56));
        Console.WriteLine(string.Create(c, $"Frequency:    {result.FrequencyHz:F1} Hz"));
        Console.WriteLine(string.Create(c, $"Amplitude:    {result.AmplitudeVpx:F2} vpx (RMS)"));
        Console.WriteLine(string.Create(c, $"Confidence:   {result.Confidence:F2}"));
        Console.WriteLine(string.Create(c, $"Samples:      {result.SampleCount}"));
        Console.WriteLine();

        // Derive config via mapper
        var profile = result.ToMotorProfile("calibrated");
        var config = ProfileToConfigMapper.Map(profile);

        Console.WriteLine("Derived Config:");
        Console.WriteLine(string.Create(c, $"  Smoothing:    strength={config.SmoothingStrength:F2}  minAlpha={config.SmoothingMinAlpha:F2}"));
        Console.WriteLine(string.Create(c, $"  Deadzone:     {config.DeadzoneRadiusVpx:F2} vpx"));
        Console.WriteLine(string.Create(c, $"  Phase comp:   {config.PhaseCompensationGainS * 1000f:F1} ms"));
        Console.WriteLine(string.Create(c, $"  Intent boost: {config.IntentBoostStrength:F2}"));

        if (exportPath is not null)
        {
            string json = JsonSerializer.Serialize(config, JsonOpts);
            File.WriteAllText(exportPath, json);
            Console.WriteLine();
            Console.WriteLine($"Config exported to {exportPath}");
        }

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
        Console.WriteLine("  --precision               Enable dual-pole precision mode (−40 dB/decade)");
        Console.WriteLine("  --targets                 Enable UI Automation target awareness");
        Console.WriteLine("  --calibrate               Run 5-second calibration session");
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
