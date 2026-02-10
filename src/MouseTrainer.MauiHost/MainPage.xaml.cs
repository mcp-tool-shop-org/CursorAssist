using System.Diagnostics;
using MouseTrainer.Audio.Assets;
using MouseTrainer.Audio.Core;
using MouseTrainer.Domain.Events;
using MouseTrainer.Domain.Input;
using MouseTrainer.Simulation.Core;
using Plugin.Maui.Audio;

namespace MouseTrainer.MauiHost;

public partial class MainPage : ContentPage
{
    private readonly DeterministicLoop _loop;
    private readonly MauiAudioSink _sink;
    private readonly AudioDirector _audio;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    private readonly RendererState _overlayState = new();
    private readonly int _fixedHz = 60;

    // --- Effects systems ---
    private readonly TrailBuffer _trailBuffer = new(16);
    private readonly ParticleSystem _particles = new(32);
    private readonly ScreenShake _shake = new();

    // --- Persistence ---
    private readonly SessionStore _store;

    private IDispatcherTimer? _timer;
    private long _frame;
    private long _prevTick;

    // --- Pointer state (sampled by host, consumed by sim) ---
    private float _latestX;
    private float _latestY;
    private bool _primaryDown;

    private const float VirtualW = 1920f;
    private const float VirtualH = 1080f;

    // --- Sandbox state ---
    private bool _running;

    public MainPage()
    {
        InitializeComponent();

        // Sandbox: no game simulation — loop ticks with a null sim
        _loop = new DeterministicLoop(new NullSimulation(), new DeterministicConfig
        {
            FixedHz = _fixedHz,
            MaxStepsPerFrame = 6,
            SessionSeed = 0
        });

        _sink = new MauiAudioSink(AudioManager.Current, log: AppendLog);
        _audio = new AudioDirector(AudioCueMap.Default(), _sink);
        _store = new SessionStore(log: AppendLog);

        // Wire effects systems into renderer state
        _overlayState.Trail = _trailBuffer;
        _overlayState.Particles = _particles;

        OverlayView.Drawable = new GameRenderer(_overlayState);
        AttachPointerInput();

        // Start in sandbox idle state
        _overlayState.SessionPhase = SandboxPhase.Idle;
        OverlayView.Invalidate();

        _ = VerifyAssetsAsync();
        AppendLog($"> Sandbox host started. FixedHz={_fixedHz}");
    }

    // ------------------------------------------------------------------
    //  Pointer input
    // ------------------------------------------------------------------

    private void AttachPointerInput()
    {
        var ptr = new PointerGestureRecognizer();

        ptr.PointerMoved += (_, e) =>
        {
            var p = e.GetPosition(GameSurface);
            if (p is null) return;
            (_latestX, _latestY) = DeviceToVirtual((float)p.Value.X, (float)p.Value.Y);

            _overlayState.CursorX = _latestX;
            _overlayState.CursorY = _latestY;
            OverlayView.Invalidate();
        };

        ptr.PointerPressed += (_, e) =>
        {
            var p = e.GetPosition(GameSurface);
            if (p is not null)
            {
                (_latestX, _latestY) = DeviceToVirtual((float)p.Value.X, (float)p.Value.Y);
                _overlayState.CursorX = _latestX;
                _overlayState.CursorY = _latestY;
            }
            _primaryDown = true;
            _overlayState.PrimaryDown = true;
            OverlayView.Invalidate();
        };

        ptr.PointerReleased += (_, _) =>
        {
            _primaryDown = false;
            _overlayState.PrimaryDown = false;
            OverlayView.Invalidate();
        };

        GameSurface.GestureRecognizers.Add(ptr);
    }

    private (float X, float Y) DeviceToVirtual(float deviceX, float deviceY)
    {
        var w = (float)GameSurface.Width;
        var h = (float)GameSurface.Height;

        if (w <= 1 || h <= 1)
            return (0f, 0f);

        var scale = MathF.Min(w / VirtualW, h / VirtualH);
        var contentW = VirtualW * scale;
        var contentH = VirtualH * scale;
        var offsetX = (w - contentW) * 0.5f;
        var offsetY = (h - contentH) * 0.5f;

        _overlayState.OffsetX = offsetX;
        _overlayState.OffsetY = offsetY;
        _overlayState.Scale = scale;

        var x = (deviceX - offsetX) / scale;
        var y = (deviceY - offsetY) / scale;

        x = MathF.Max(0f, MathF.Min(VirtualW, x));
        y = MathF.Max(0f, MathF.Min(VirtualH, y));

        return (x, y);
    }

    private PointerInput SamplePointer()
        => new PointerInput(_latestX, _latestY, _primaryDown, false, _stopwatch.ElapsedTicks);

    // ------------------------------------------------------------------
    //  Sandbox controls
    // ------------------------------------------------------------------

    private void OnActionClicked(object sender, EventArgs e)
    {
        if (_running)
            StopSandbox();
        else
            StartSandbox();
    }

    private void StartSandbox()
    {
        _loop.Reset(0);
        _frame = 0;
        _prevTick = 0;
        _trailBuffer.Clear();
        _particles.Clear();
        _shake.Clear();
        _running = true;

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(16);
        _timer.Tick += (_, _) => StepOnce();
        _timer.Start();

        _overlayState.SessionPhase = SandboxPhase.Running;
        ActionButton.Text = "Stop";
        ActionButton.BackgroundColor = Color.FromArgb("#FF5722");
        StatusLabel.Text = "Running";
        StatusLabel.TextColor = Color.FromArgb("#4CAF50");

        AppendLog("> Sandbox started.");
    }

    private void StopSandbox()
    {
        StopTimer();
        _running = false;

        _overlayState.SessionPhase = SandboxPhase.Idle;
        ActionButton.Text = "Start";
        ActionButton.BackgroundColor = Color.FromArgb("#4CAF50");
        StatusLabel.Text = "Idle";
        StatusLabel.TextColor = Color.FromArgb("#888888");

        OverlayView.Invalidate();
        AppendLog("> Sandbox stopped.");
    }

    private void StopTimer()
    {
        if (_timer is null) return;
        _timer.Stop();
        _timer = null;
    }

    // ------------------------------------------------------------------
    //  Simulation loop (sandbox — cursor + trail + effects, no game events)
    // ------------------------------------------------------------------

    private void StepOnce()
    {
        if (!_running) return;

        var input = SamplePointer();
        var nowTicks = _stopwatch.ElapsedTicks;
        var result = _loop.Step(input, nowTicks, Stopwatch.Frequency);

        int ticksAdvanced = (int)(result.Tick - _prevTick);
        _prevTick = result.Tick;

        float alpha = Math.Clamp(result.Alpha, 0f, 1f);
        float simTime = (result.Tick + alpha) * (1f / _fixedHz);
        float dt = 1f / _fixedHz;

        // Update renderer state
        _overlayState.Tick = result.Tick;
        _overlayState.SimTime = simTime;
        _overlayState.Alpha = result.Alpha;

        // Push cursor trail point
        _trailBuffer.Push(_latestX, _latestY, simTime);

        // Update effects systems
        _particles.Update(dt);
        _shake.Update(dt, _overlayState);

        _frame++;
        if (_frame % 2 == 0)
            OverlayView.Invalidate();

        if (_frame % 300 == 0)
            AppendLog($"> tick={result.Tick} pos=({_latestX:0},{_latestY:0}) primary={_primaryDown}");
    }

    // ------------------------------------------------------------------
    //  Assets
    // ------------------------------------------------------------------

    private async Task VerifyAssetsAsync()
    {
        try
        {
            var missing = await AssetVerifier.VerifyRequiredAudioAsync(new MauiAssetOpener(), CancellationToken.None);
            if (missing.Count == 0)
                AppendLog("> Assets OK.");
            else
            {
                AppendLog($"> MISSING {missing.Count} assets:");
                foreach (var m in missing) AppendLog($"  - {m}");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"> Asset error: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void AppendLog(string line)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LogLabel.Text += line + Environment.NewLine;
        });
    }
}

/// <summary>
/// No-op simulation for sandbox mode. The deterministic loop ticks but no game events are produced.
/// </summary>
internal sealed class NullSimulation : IGameSimulation
{
    public void Reset(uint sessionSeed) { }
    public void FixedUpdate(long tick, float dt, in PointerInput input, List<GameEvent> events) { }
}
