using System.Globalization;
using CursorAssist.Canon.Schemas;
using CursorAssist.Runtime.Core;

namespace CursorAssist.Pilot;

/// <summary>
/// Background thread that refreshes the console with real-time engine state.
/// Updates at ~2 Hz using Console.SetCursorPosition for in-place refresh.
///
/// Display:
///   CursorAssist Pilot v0.1  |  Session: abc123  |  00:05:23 elapsed
///   ────────────────────────────────────────────────────────
///   Cursor:    X=1234.5  Y=567.8  |  Vel: 4.2 vpx/tick
///   Engine:    Running  |  Tick: 19,234  |  Overruns: 0
///   Config:    SmoothStr=0.60  MinAlpha=0.25  DZ=1.50  PhaseComp=0.008
///   Queues:    Input=0  Injection=0
///   Kill:      Armed  |  Ctrl+Shift+Pause
///   ────────────────────────────────────────────────────────
///   Press Ctrl+C to stop session
/// </summary>
public sealed class ConsoleDashboard : IDisposable
{
    private readonly EngineThread _engine;
    private readonly AssistiveConfig _config;
    private readonly string _sessionId;
    private readonly DateTimeOffset _sessionStarted;
    private readonly IKillSwitch _killSwitch;

    private Thread? _thread;
    private volatile bool _running;
    private bool _disposed;

    // Dashboard starts writing at this console line (after startup banner)
    private int _dashboardStartLine;

    public ConsoleDashboard(
        EngineThread engine,
        AssistiveConfig config,
        string sessionId,
        DateTimeOffset sessionStarted,
        IKillSwitch killSwitch)
    {
        _engine = engine;
        _config = config;
        _sessionId = sessionId;
        _sessionStarted = sessionStarted;
        _killSwitch = killSwitch;
    }

    public void Start()
    {
        if (_running) return;

        // Record current console position as dashboard anchor
        _dashboardStartLine = Console.CursorTop;
        _running = true;
        _thread = new Thread(RefreshLoop)
        {
            Name = "CursorAssist.Dashboard",
            IsBackground = true
        };
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        _thread?.Join(timeout: TimeSpan.FromSeconds(2));
        _thread = null;
    }

    private void RefreshLoop()
    {
        while (_running)
        {
            try
            {
                Render();
            }
            catch (IOException)
            {
                // Console output interrupted — terminal closed or redirected
                _running = false;
                break;
            }

            Thread.Sleep(500); // ~2 Hz refresh
        }
    }

    private void Render()
    {
        var elapsed = DateTimeOffset.UtcNow - _sessionStarted;
        var cursor = _engine.Cursor;
        float velMag = MathF.Sqrt(cursor.VelocityX * cursor.VelocityX +
                                  cursor.VelocityY * cursor.VelocityY);
        // Convert vpx/s back to vpx/tick for display
        float velPerTick = velMag / 60f;

        string state = _engine.IsRunning ? "Running" : "Stopped";
        string killState = _killSwitch.IsArmed ? "Armed" : "Disarmed";

        // Use invariant culture for all formatting
        var c = CultureInfo.InvariantCulture;

        string line0 = string.Create(c,
            $"CursorAssist Pilot v0.1  |  Session: {_sessionId}  |  {elapsed:hh\\:mm\\:ss} elapsed");

        string separator = new('\u2500', 58);

        string line2 = string.Create(c,
            $"Cursor:    X={cursor.X,8:F1}  Y={cursor.Y,8:F1}  |  Vel: {velPerTick,5:F2} vpx/tick");

        string line3 = string.Create(c,
            $"Engine:    {state,-7}  |  Overruns: {_engine.OverrunCount}");

        string line4 = string.Create(c,
            $"Config:    Smooth={_config.SmoothingStrength:F2}  MinA={_config.SmoothingMinAlpha:F2}  " +
            $"DZ={_config.DeadzoneRadiusVpx:F2}  PC={_config.PhaseCompensationGainS:F3}");

        string line5 = string.Create(c,
            $"Queues:    Input={_engine.InputQueue.Count}  Injection={_engine.InjectionQueue.Count}");

        string line6 = string.Create(c,
            $"Kill:      {killState}  |  Ctrl+Shift+Pause");

        string line8 = "Press Ctrl+C to stop session";

        // Write lines in-place
        int consoleWidth = 80;
        try
        {
            consoleWidth = Console.WindowWidth;
        }
        catch (IOException)
        {
            // Redirected console — no window width available
        }

        Console.SetCursorPosition(0, _dashboardStartLine);
        WritePadded(line0, consoleWidth);
        WritePadded(separator, consoleWidth);
        WritePadded(line2, consoleWidth);
        WritePadded(line3, consoleWidth);
        WritePadded(line4, consoleWidth);
        WritePadded(line5, consoleWidth);
        WritePadded(line6, consoleWidth);
        WritePadded(separator, consoleWidth);
        WritePadded(line8, consoleWidth);
    }

    private static void WritePadded(string text, int width)
    {
        // Pad to fill the full width (clears leftover chars from previous render)
        if (text.Length < width)
            Console.WriteLine(text + new string(' ', width - text.Length));
        else
            Console.WriteLine(text[..width]);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
