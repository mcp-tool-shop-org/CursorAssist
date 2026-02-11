namespace CursorAssist.Engine.Analysis;

/// <summary>
/// Standalone tremor frequency and amplitude analyzer.
/// Extracted from SmoothingTransform's nested TremorFrequencyEstimator
/// for reuse in calibration sessions and diagnostic tools.
///
/// Algorithm (identical to SmoothingTransform's estimator):
///   1. High-pass pre-filter: delta − EMA_slow(delta) isolates tremor band
///   2. Zero-crossing counter over sliding 1-second window (60 ticks)
///   3. EMA on raw frequency estimate for temporal stability
///   4. Safeguards: frequency band [3, 15] Hz, velocity gate, amplitude floor
///   5. RMS amplitude tracking on high-pass filtered deltas
///
/// Closed-form output:
///   α_min = Clamp(FreqToAlphaK × f_est, 0.20, 0.40)
///   where FreqToAlphaK = 2π × 0.5 / 60 ≈ 0.05236
///
/// O(1) per tick. Fixed-size circular buffer, no per-tick allocations.
/// </summary>
public sealed class TremorAnalyzer
{
    // ── Constants ──

    /// <summary>60 ticks = 1 second at 60 Hz. Provides 1 Hz frequency resolution.</summary>
    private const int WindowSize = 60;

    /// <summary>EMA alpha for slow baseline (~1 Hz cutoff at 60 Hz): α ≈ 2π·1/60 ≈ 0.105.</summary>
    private const float SlowEmaAlpha = 0.105f;

    /// <summary>EMA beta for frequency smoothing (slow adaptation, prevents jitter).</summary>
    private const float FreqEmaBeta = 0.1f;

    /// <summary>Minimum valid tremor frequency (Hz). Below this → intentional motion.</summary>
    internal const float MinFreqHz = 3f;

    /// <summary>Maximum valid tremor frequency (Hz). Above this → noise/aliasing.</summary>
    internal const float MaxFreqHz = 15f;

    /// <summary>Minimum high-pass magnitude to count as a real crossing (noise floor, vpx).</summary>
    private const float AmplitudeFloor = 0.15f;

    /// <summary>Velocity gate threshold (vpx/tick). Freeze adaptation above this.</summary>
    private const float VelocityGateThreshold = 4f;

    /// <summary>Consecutive ticks above gate to trigger freeze (~200ms at 60 Hz).</summary>
    private const int VelocityGateTickCount = 12;

    /// <summary>Closed-form mapping: 2π × 0.5 / 60 ≈ 0.05236.</summary>
    internal const float FreqToAlphaK = 0.05236f;

    /// <summary>EMA beta for amplitude tracking.</summary>
    private const float AmplitudeEmaBeta = 0.05f;

    // ── State ──

    // Circular buffer: +1 = positive HP, -1 = negative, 0 = below amplitude floor
    private readonly sbyte[] _signBuffer = new sbyte[WindowSize];
    private int _bufferIndex;
    private int _bufferCount;

    // Running zero-crossing count within the window
    private int _crossingCount;

    // Slow-EMA state for high-pass filter
    private float _slowEmaX;
    private float _slowEmaY;

    // EMA state for frequency estimate
    private float _freqEstimate;

    // Velocity gate counter
    private int _highVelocityTicks;

    // Amplitude tracking (RMS via EMA of squared HP magnitude)
    private float _amplitudeSquaredEma;

    private bool _initialized;
    private int _totalTicks;

    /// <summary>Current estimated tremor frequency in Hz.</summary>
    public float FrequencyHz => _freqEstimate;

    /// <summary>Current estimated tremor amplitude in vpx (RMS).</summary>
    public float AmplitudeVpx => MathF.Sqrt(_amplitudeSquaredEma);

    /// <summary>Total ticks processed.</summary>
    public int TotalTicks => _totalTicks;

    /// <summary>
    /// Dynamic minAlpha derived from frequency estimate.
    /// Returns ≥ 0 if valid, or −1 if no valid estimate (use static fallback).
    /// </summary>
    public float DynamicMinAlpha =>
        _freqEstimate >= MinFreqHz
            ? Math.Clamp(FreqToAlphaK * _freqEstimate, 0.20f, 0.40f)
            : -1f;

    /// <summary>Seed the analyzer with an initial frequency from the motor profile.</summary>
    public void Initialize(float seedFrequencyHz)
    {
        _freqEstimate = seedFrequencyHz > 0f ? seedFrequencyHz : 0f;
        _bufferIndex = 0;
        _bufferCount = 0;
        _crossingCount = 0;
        _slowEmaX = 0f;
        _slowEmaY = 0f;
        _highVelocityTicks = 0;
        _amplitudeSquaredEma = 0f;
        _initialized = false;
        _totalTicks = 0;
        Array.Clear(_signBuffer);
    }

    /// <summary>Update with this tick's raw deltas and velocity magnitude.</summary>
    public void Update(float dx, float dy, float velocity)
    {
        _totalTicks++;

        if (!_initialized)
        {
            _slowEmaX = dx;
            _slowEmaY = dy;
            _initialized = true;
            return;
        }

        // ── Velocity gate ──
        if (velocity > VelocityGateThreshold)
        {
            _highVelocityTicks++;
            if (_highVelocityTicks >= VelocityGateTickCount)
                return; // Freeze: don't update during sustained fast motion
        }
        else
        {
            _highVelocityTicks = 0;
        }

        // ── High-pass pre-filter ──
        _slowEmaX += SlowEmaAlpha * (dx - _slowEmaX);
        _slowEmaY += SlowEmaAlpha * (dy - _slowEmaY);

        float hpX = dx - _slowEmaX;
        float hpY = dy - _slowEmaY;
        float hpMag = MathF.Sqrt(hpX * hpX + hpY * hpY);

        // ── Track amplitude (RMS via EMA of squared magnitude) ──
        _amplitudeSquaredEma += AmplitudeEmaBeta * (hpMag * hpMag - _amplitudeSquaredEma);

        // ── Determine sign for zero-crossing ──
        sbyte newSign;
        if (hpMag < AmplitudeFloor)
        {
            newSign = 0; // Below noise floor
        }
        else
        {
            // Dominant-axis sign (avoids false crossings from 2D projection)
            float dominant = MathF.Abs(hpX) >= MathF.Abs(hpY) ? hpX : hpY;
            newSign = dominant >= 0f ? (sbyte)1 : (sbyte)-1;
        }

        // ── Update circular buffer and crossing count ──

        // Remove outgoing sample's crossing contribution when buffer is full
        if (_bufferCount >= WindowSize)
        {
            int outIdx = _bufferIndex;
            int nextIdx = (outIdx + 1) % WindowSize;
            sbyte outgoing = _signBuffer[outIdx];
            sbyte successor = _signBuffer[nextIdx];

            if (outgoing != 0 && successor != 0 && outgoing != successor)
            {
                _crossingCount--;
            }
        }

        // Check if new sample forms a crossing with previous
        if (_bufferCount > 0)
        {
            int prevIdx = (_bufferIndex - 1 + WindowSize) % WindowSize;
            sbyte prev = _signBuffer[prevIdx];

            if (prev != 0 && newSign != 0 && prev != newSign)
            {
                _crossingCount++;
            }
        }

        // Write new sample
        _signBuffer[_bufferIndex] = newSign;
        _bufferIndex = (_bufferIndex + 1) % WindowSize;
        if (_bufferCount < WindowSize)
            _bufferCount++;

        // ── Compute raw frequency estimate ──
        // Need at least half a window (30 ticks = 0.5s)
        if (_bufferCount >= WindowSize / 2)
        {
            float durationS = _bufferCount / 60f;
            float rawFreq = (_crossingCount * 0.5f) / durationS;

            // Only update if in valid tremor band
            if (rawFreq >= MinFreqHz && rawFreq <= MaxFreqHz)
            {
                _freqEstimate += FreqEmaBeta * (rawFreq - _freqEstimate);
            }
        }
    }

    /// <summary>Reset all analyzer state.</summary>
    public void Reset()
    {
        Initialize(0f);
    }
}
