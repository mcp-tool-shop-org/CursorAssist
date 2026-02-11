using CursorAssist.Engine.Metrics;

namespace CursorAssist.Engine.Core;

/// <summary>
/// The generalized deterministic input pipeline. Fixed-timestep accumulator loop
/// that feeds raw input through a transform chain and emits EngineFrameResults.
///
/// This is the CursorAssist equivalent of MouseTrainer's DeterministicLoop,
/// generalized for assist, benchmark, and training expressions.
/// </summary>
public sealed class DeterministicPipeline
{
    private readonly TransformPipeline _transforms;
    private readonly IMetricsSink _metrics;
    private readonly int _fixedHz;
    private readonly int _maxStepsPerFrame;

    private readonly double _fixedDtD;
    private readonly float _fixedDt;

    private long _tick;
    private double _accumulatorSeconds;
    private long _lastHostTicks;
    private ulong _deterministicHash;

    private readonly List<EngineEvent> _events = new(capacity: 32);

    // FNV-1a constants (same as MouseTrainer.Domain.Utility.Fnv1a)
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    public long CurrentTick => _tick;
    public ulong DeterminismHash => _deterministicHash;

    public DeterministicPipeline(
        TransformPipeline transforms,
        IMetricsSink? metrics = null,
        int fixedHz = 60,
        int maxStepsPerFrame = 5)
    {
        _transforms = transforms;
        _metrics = metrics ?? NullMetricsSink.Instance;
        _fixedHz = fixedHz;
        _maxStepsPerFrame = maxStepsPerFrame;
        _fixedDtD = 1.0 / fixedHz;
        _fixedDt = 1f / fixedHz;
        _deterministicHash = FnvOffsetBasis;
    }

    public void Reset()
    {
        _tick = 0;
        _accumulatorSeconds = 0;
        _lastHostTicks = 0;
        _deterministicHash = FnvOffsetBasis;
        _transforms.Reset();
        _metrics.Reset();
    }

    /// <summary>
    /// Step the pipeline with host-provided input and time.
    /// Returns the frame result with final cursor position, events, and hash.
    /// </summary>
    /// <param name="raw">Raw input sample (position, deltas, buttons).</param>
    /// <param name="targets">Active targets for this frame.</param>
    /// <param name="hostNowTicks">Host monotonic clock ticks.</param>
    /// <param name="ticksPerSecond">Host ticks per second (e.g., Stopwatch.Frequency).</param>
    /// <param name="context">Transform context (profile, config, targets).</param>
    public EngineFrameResult Step(
        in InputSample raw,
        IReadOnlyList<TargetInfo> targets,
        long hostNowTicks,
        double ticksPerSecond,
        TransformContext? context = null)
    {
        if (_lastHostTicks == 0)
        {
            _lastHostTicks = hostNowTicks;
            return MakeResult(raw, raw, 0f);
        }

        var deltaTicks = hostNowTicks - _lastHostTicks;
        _lastHostTicks = hostNowTicks;

        var deltaSeconds = deltaTicks / ticksPerSecond;
        if (deltaSeconds < 0) deltaSeconds = 0;

        _accumulatorSeconds += deltaSeconds;
        _events.Clear();

        var lastTransformed = raw;
        int steps = 0;

        while (_accumulatorSeconds >= _fixedDtD && steps < _maxStepsPerFrame)
        {
            var ctx = context ?? new TransformContext
            {
                Tick = _tick,
                Dt = _fixedDt,
                Targets = targets
            };

            lastTransformed = _transforms.Apply(in raw, ctx);

            // Hash the transformed output for determinism verification
            _deterministicHash = HashSample(_deterministicHash, in lastTransformed);

            _metrics.RecordTick(_tick, in raw, in lastTransformed, targets);

            _tick++;
            _accumulatorSeconds -= _fixedDtD;
            steps++;
        }

        var alpha = (float)(_accumulatorSeconds / _fixedDtD);
        if (alpha < 0f) alpha = 0f;
        if (alpha >= 1f) alpha = 0.9999f;

        return MakeResult(raw, lastTransformed, alpha);
    }

    /// <summary>
    /// Simplified tick-by-tick stepping for benchmarks and replays.
    /// No host clock — advances exactly one tick per call.
    /// </summary>
    public EngineFrameResult FixedStep(in InputSample raw, TransformContext context)
    {
        _events.Clear();

        var transformed = _transforms.Apply(in raw, context);
        _deterministicHash = HashSample(_deterministicHash, in transformed);
        _metrics.RecordTick(_tick, in raw, in transformed, context.Targets);

        _tick++;

        return new EngineFrameResult
        {
            Tick = _tick - 1,
            FinalCursor = transformed,
            RawInput = raw,
            Events = _events.ToArray(),
            Alpha = 0f,
            DeterminismHash = _deterministicHash
        };
    }

    /// <summary>Emit an event from outside the pipeline (e.g., mode logic).</summary>
    public void EmitEvent(in EngineEvent evt)
    {
        _events.Add(evt);
        _metrics.RecordEvent(in evt);
    }

    private EngineFrameResult MakeResult(in InputSample raw, in InputSample transformed, float alpha)
    {
        return new EngineFrameResult
        {
            Tick = _tick,
            FinalCursor = transformed,
            RawInput = raw,
            Events = _events.ToArray(),
            Alpha = alpha,
            DeterminismHash = _deterministicHash
        };
    }

    private static ulong HashSample(ulong hash, in InputSample s)
    {
        // Hash X, Y as IEEE 754 bits for platform stability
        hash = HashFloat(hash, s.X);
        hash = HashFloat(hash, s.Y);
        hash = HashByte(hash, s.PrimaryDown ? (byte)1 : (byte)0);
        hash = HashByte(hash, s.SecondaryDown ? (byte)1 : (byte)0);
        return hash;
    }

    private static ulong HashFloat(ulong hash, float value)
    {
        uint bits = BitConverter.SingleToUInt32Bits(value);
        hash = HashByte(hash, (byte)bits);
        hash = HashByte(hash, (byte)(bits >> 8));
        hash = HashByte(hash, (byte)(bits >> 16));
        hash = HashByte(hash, (byte)(bits >> 24));
        return hash;
    }

    private static ulong HashByte(ulong hash, byte b)
    {
        hash ^= b;
        hash *= FnvPrime;
        return hash;
    }
}
