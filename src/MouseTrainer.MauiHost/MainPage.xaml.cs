using System.Diagnostics;
using MouseTrainer.Core.Assets;
using MouseTrainer.Core.Audio;
using MouseTrainer.Core.Input;
using MouseTrainer.Core.Simulation;
using MouseTrainer.Core.Simulation.ReflexGates;

namespace MouseTrainer.MauiHost;

public partial class MainPage : ContentPage
{
    private readonly DeterministicLoop _loop;
    private readonly AudioDirector _audio;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    private IDispatcherTimer? _timer;
    private long _frame;

    // --- Pointer state (sampled by host, consumed by sim) ---
    private float _latestX;
    private float _latestY;
    private bool _primaryDown;

    private const float VirtualW = 1920f;
    private const float VirtualH = 1080f;

    public MainPage()
    {
        InitializeComponent();

        _loop = new DeterministicLoop(new ReflexGateSimulation(new ReflexGateConfig()), new DeterministicConfig
        {
            FixedHz = 60,
            MaxStepsPerFrame = 6,
            SessionSeed = 0xC0FFEEu
        });

        _audio = new AudioDirector(AudioCueMap.Default(), new LogAudioSink(AppendLog));

        AttachPointerInput();
        _ = VerifyAssetsAsync();
        AppendLog($"> Host started. Stopwatch freq: {Stopwatch.Frequency} ticks/sec");
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
            UpdatePointerLabel();
        };

        ptr.PointerPressed += (_, e) =>
        {
            var p = e.GetPosition(GameSurface);
            if (p is not null)
                (_latestX, _latestY) = DeviceToVirtual((float)p.Value.X, (float)p.Value.Y);
            _primaryDown = true;
        };

        ptr.PointerReleased += (_, _) =>
        {
            _primaryDown = false;
        };

        GameSurface.GestureRecognizers.Add(ptr);
    }

    private (float X, float Y) DeviceToVirtual(float deviceX, float deviceY)
    {
        var w = (float)GameSurface.Width;
        var h = (float)GameSurface.Height;

        if (w <= 1 || h <= 1)
            return (0f, 0f);

        // Letterbox-aware mapping: maintain aspect ratio
        var scale = MathF.Min(w / VirtualW, h / VirtualH);
        var contentW = VirtualW * scale;
        var contentH = VirtualH * scale;
        var offsetX = (w - contentW) * 0.5f;
        var offsetY = (h - contentH) * 0.5f;

        var x = (deviceX - offsetX) / scale;
        var y = (deviceY - offsetY) / scale;

        // Clamp to playfield
        x = MathF.Max(0f, MathF.Min(VirtualW, x));
        y = MathF.Max(0f, MathF.Min(VirtualH, y));

        return (x, y);
    }

    private PointerInput SamplePointer()
    {
        return new PointerInput(_latestX, _latestY, _primaryDown, false, _stopwatch.ElapsedTicks);
    }

    private void UpdatePointerLabel()
    {
        PointerLabel.Text = $"Y: {_latestY:0}  (virtual 0–1080)";
    }

    // ------------------------------------------------------------------
    //  Simulation loop
    // ------------------------------------------------------------------

    private void OnStartClicked(object sender, EventArgs e)
    {
        if (_timer is not null) return;

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(16);
        _timer.Tick += (_, _) => StepOnce();
        _timer.Start();

        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        StatusLabel.Text = "Running";
        StatusLabel.TextColor = Color.FromArgb("#4CAF50");

        AppendLog("> Loop started.");
    }

    private void OnStopClicked(object sender, EventArgs e)
    {
        if (_timer is null) return;

        _timer.Stop();
        _timer = null;

        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        StatusLabel.Text = "Stopped";
        StatusLabel.TextColor = Color.FromArgb("#888888");

        AppendLog("> Loop stopped.");
    }

    private void OnClearClicked(object sender, EventArgs e)
    {
        LogLabel.Text = "";
    }

    private void StepOnce()
    {
        var input = SamplePointer();
        var nowTicks = _stopwatch.ElapsedTicks;
        var result = _loop.Step(input, nowTicks, Stopwatch.Frequency);

        if (result.Events.Count > 0)
            _audio.Process(result.Events, result.Tick, sessionSeed: 0xC0FFEEu);

        _frame++;
        if (_frame % 60 == 0)
        {
            AppendLog($"> tick={result.Tick} Y={_latestY:0} events={result.Events.Count}");
        }
    }

    // ------------------------------------------------------------------
    //  Asset verification
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
