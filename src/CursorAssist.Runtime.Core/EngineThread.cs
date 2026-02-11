using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CursorAssist.Canon.Schemas;
using CursorAssist.Engine.Core;
using CursorAssist.Engine.Metrics;

[assembly: InternalsVisibleTo("CursorAssist.Tests")]

namespace CursorAssist.Runtime.Core;

/// <summary>
/// The real-time engine thread. Owns the deterministic pipeline,
/// accumulator, and config state. Reads from the input queue,
/// writes to the injection queue.
///
/// Threading contract:
///   - OS Input Thread -> _inputQueue (lock-free)
///   - Engine Thread (this) -> _injectionQueue (lock-free)
///   - Injection Thread reads _injectionQueue
///
/// Config swap is atomic at frame boundary.
/// </summary>
public sealed class EngineThread : IDisposable
{
    private readonly ConcurrentQueue<RawInputEvent> _inputQueue = new();
    private readonly ConcurrentQueue<AssistedDelta> _injectionQueue = new();

    private readonly TransformPipeline _pipeline;
    private readonly DeterministicPipeline _engine;
    private readonly int _fixedHz;
    private readonly float _fixedDt;
    private readonly double _fixedDtD;
    private readonly int _maxStepsPerFrame;

    private volatile AssistiveConfig? _pendingConfig;
    private AssistiveConfig? _activeConfig;
    private MotorProfile? _activeProfile;

    private CursorState _cursor;
    private Thread? _thread;
    private volatile bool _running;
    private bool _disposed;
    private long _overrunCount;

    // Injection tagging: ring buffer of last N injected deltas with timestamps.
    // The time window prevents stale entries from false-matching legitimate user deltas.
    private const int InjectedRingSize = 8;
    private static readonly long EchoWindowTicks = Stopwatch.Frequency / 20; // 50ms
    private readonly (float dx, float dy, long timestamp)[] _injectedRing =
        new (float, float, long)[InjectedRingSize];
    private int _injectedRingIdx;

    public ConcurrentQueue<RawInputEvent> InputQueue => _inputQueue;
    public ConcurrentQueue<AssistedDelta> InjectionQueue => _injectionQueue;
    public bool IsRunning => _running;
    public CursorState Cursor => _cursor;
    public long OverrunCount => _overrunCount;

    public EngineThread(
        TransformPipeline pipeline,
        IMetricsSink? metrics = null,
        int fixedHz = 60,
        int maxStepsPerFrame = 5)
    {
        _pipeline = pipeline;
        _fixedHz = fixedHz;
        _fixedDt = 1f / fixedHz;
        _fixedDtD = 1.0 / fixedHz;
        _maxStepsPerFrame = maxStepsPerFrame;
        _engine = new DeterministicPipeline(pipeline, metrics, fixedHz, maxStepsPerFrame);
    }

    /// <summary>
    /// Enable the runtime. Initializes cursor state from provided OS position.
    /// </summary>
    public void Enable(float initialX, float initialY, AssistiveConfig? config = null)
    {
        if (_running) return;

        _cursor.Reset(initialX, initialY);
        _activeConfig = config;
        _pendingConfig = null;
        _engine.Reset();

        // Drain queues
        while (_inputQueue.TryDequeue(out _)) { }
        while (_injectionQueue.TryDequeue(out _)) { }

        _running = true;
        _thread = new Thread(RunLoop)
        {
            Name = "CursorAssist.EngineThread",
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal
        };
        _thread.Start();
    }

    /// <summary>
    /// Disable the runtime. Stops the engine thread.
    /// </summary>
    public void Disable()
    {
        _running = false;
        _thread?.Join(timeout: TimeSpan.FromSeconds(2));
        _thread = null;
    }

    /// <summary>
    /// Emergency stop: halt engine, drain all queues, reset pipeline and cursor.
    /// Designed to be called from the kill switch event handler.
    /// Safe to call from any thread.
    /// </summary>
    public void EmergencyStop()
    {
        Disable();

        // Drain queues
        while (_inputQueue.TryDequeue(out _)) { }
        while (_injectionQueue.TryDequeue(out _)) { }

        // Reset pipeline state
        _engine.Reset();

        // Reset cursor
        _cursor.Reset(0f, 0f);

        // Clear injection ring buffer
        Array.Clear(_injectedRing);
        _injectedRingIdx = 0;

        // Clear config
        _activeConfig = null;
        _pendingConfig = null;
    }

    /// <summary>
    /// Atomic config swap. Takes effect at next frame boundary.
    /// </summary>
    public void UpdateConfig(AssistiveConfig config)
    {
        _pendingConfig = config;
    }

    /// <summary>
    /// Update the motor profile (for context-aware transforms).
    /// </summary>
    public void UpdateProfile(MotorProfile profile)
    {
        _activeProfile = profile;
    }

    /// <summary>
    /// Check if a delta was recently injected (loop prevention guard layer 2).
    /// Dual check: delta values must match within tolerance AND the entry must be
    /// within the echo time window (50ms). Entries older than the window are ignored
    /// to prevent stale ring buffer entries from false-matching legitimate user deltas.
    /// </summary>
    public bool WasRecentlyInjected(float dx, float dy, long nowTicks, float tolerance = 0.01f)
    {
        for (int i = 0; i < _injectedRing.Length; i++)
        {
            var (rx, ry, ts) = _injectedRing[i];
            if (ts == 0) continue; // Uninitialized slot
            if (nowTicks - ts > EchoWindowTicks) continue; // Outside time window
            if (MathF.Abs(rx - dx) < tolerance && MathF.Abs(ry - dy) < tolerance)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Record an injected delta in the ring buffer for echo guard matching.
    /// </summary>
    internal void RecordInjectedDelta(float dx, float dy, long timestampTicks)
    {
        _injectedRing[_injectedRingIdx % InjectedRingSize] = (dx, dy, timestampTicks);
        _injectedRingIdx++;
    }

    /// <summary>
    /// Run the pipeline over a recorded stream (for golden hash tests).
    /// Synchronous, no threads.
    /// </summary>
    public ulong ReplayStream(IReadOnlyList<RawInputEvent> events)
    {
        _engine.Reset();

        float curX = 0f, curY = 0f;

        for (int i = 0; i < events.Count; i++)
        {
            var evt = events[i];
            curX += evt.Dx;
            curY += evt.Dy;

            // Atomic config swap at frame boundary with runtime safety clamp
            if (_pendingConfig is not null)
            {
                _activeConfig = ClampConfig(_pendingConfig);
                _pendingConfig = null;
            }

            var input = new InputSample(curX, curY, evt.Dx, evt.Dy,
                evt.PrimaryDown, evt.SecondaryDown, i);

            var ctx = new TransformContext
            {
                Tick = i,
                Dt = _fixedDt,
                Config = _activeConfig,
                Profile = _activeProfile
            };

            _engine.FixedStep(in input, ctx);
        }

        return _engine.DeterminismHash;
    }

    private void RunLoop()
    {
        var sw = Stopwatch.StartNew();
        double accumulator = 0;
        long lastTicks = sw.ElapsedTicks;
        double ticksPerSecond = Stopwatch.Frequency;

        while (_running)
        {
            long nowTicks = sw.ElapsedTicks;
            double deltaS = (nowTicks - lastTicks) / ticksPerSecond;
            lastTicks = nowTicks;

            if (deltaS < 0) deltaS = 0;
            accumulator += deltaS;

            // Atomic config swap at frame boundary with runtime safety clamp
            if (_pendingConfig is not null)
            {
                _activeConfig = ClampConfig(_pendingConfig);
                _pendingConfig = null;
            }

            // Aggregate all raw input received since last tick
            float aggDx = 0f, aggDy = 0f;
            bool primary = _cursor.PrimaryDown;
            bool secondary = _cursor.SecondaryDown;

            while (_inputQueue.TryDequeue(out var raw))
            {
                aggDx += raw.Dx;
                aggDy += raw.Dy;
                primary = raw.PrimaryDown;
                secondary = raw.SecondaryDown;
            }

            int steps = 0;
            while (accumulator >= _fixedDtD && steps < _maxStepsPerFrame)
            {
                // Apply aggregated deltas to virtual cursor
                float rawX = _cursor.X + aggDx;
                float rawY = _cursor.Y + aggDy;

                var input = new InputSample(rawX, rawY, aggDx, aggDy,
                    primary, secondary, _engine.CurrentTick);

                var ctx = new TransformContext
                {
                    Tick = _engine.CurrentTick,
                    Dt = _fixedDt,
                    Config = _activeConfig,
                    Profile = _activeProfile
                };

                var result = _engine.FixedStep(in input, ctx);

                // Compute assisted delta, clamped to prevent runaway
                float assistedDx = result.FinalCursor.X - _cursor.X;
                float assistedDy = result.FinalCursor.Y - _cursor.Y;
                (assistedDx, assistedDy) = ClampDelta(assistedDx, assistedDy);

                // Update cursor state
                _cursor.X = result.FinalCursor.X;
                _cursor.Y = result.FinalCursor.Y;
                _cursor.VelocityX = assistedDx * _fixedHz;
                _cursor.VelocityY = assistedDy * _fixedHz;
                _cursor.PrimaryDown = primary;
                _cursor.SecondaryDown = secondary;

                // Enqueue injection delta
                if (MathF.Abs(assistedDx) > 0.001f || MathF.Abs(assistedDy) > 0.001f)
                {
                    _injectionQueue.Enqueue(new AssistedDelta(assistedDx, assistedDy, _engine.CurrentTick));

                    // Record in ring buffer for echo guard (loop prevention layer 2)
                    RecordInjectedDelta(assistedDx, assistedDy, Stopwatch.GetTimestamp());
                }

                // Only apply aggregated deltas on first step; subsequent steps use zero delta
                aggDx = 0f;
                aggDy = 0f;

                accumulator -= _fixedDtD;
                steps++;
            }

            // Discard excess accumulator to prevent catch-up burst after stalls.
            // Keeps at most 1 tick of carryover (normal fractional-frame behavior).
            if (accumulator > _fixedDtD)
            {
                _overrunCount++;
                accumulator = _fixedDtD;
            }

            // Sleep to avoid busy-waiting (aim for ~1ms resolution)
            Thread.Sleep(1);
        }
    }

    /// <summary>
    /// Clamp assisted delta to prevent runaway cursor.
    /// Last line of defense: caps per-tick injection regardless of transform output.
    /// </summary>
    internal static (float dx, float dy) ClampDelta(float dx, float dy)
    {
        dx = Math.Clamp(dx, -RuntimeLimits.MaxDeltaPerTick, RuntimeLimits.MaxDeltaPerTick);
        dy = Math.Clamp(dy, -RuntimeLimits.MaxDeltaPerTick, RuntimeLimits.MaxDeltaPerTick);
        return (dx, dy);
    }

    /// <summary>
    /// Runtime-enforce config parameter bounds. Catches configs that bypass CanonValidator
    /// (e.g., direct construction, deserialization without validation).
    /// </summary>
    internal static AssistiveConfig ClampConfig(AssistiveConfig config) => config with
    {
        SmoothingMinAlpha = Math.Clamp(config.SmoothingMinAlpha, RuntimeLimits.MinAlpha, RuntimeLimits.MaxAlpha),
        SmoothingMaxAlpha = Math.Clamp(config.SmoothingMaxAlpha, RuntimeLimits.MinAlpha, RuntimeLimits.MaxAlpha),
        DeadzoneRadiusVpx = Math.Clamp(config.DeadzoneRadiusVpx, 0f, RuntimeLimits.MaxDeadzoneRadius),
        PhaseCompensationGainS = Math.Clamp(config.PhaseCompensationGainS, 0f, RuntimeLimits.MaxPhaseCompGainS)
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disable();
    }
}
