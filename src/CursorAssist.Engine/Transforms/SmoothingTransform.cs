using CursorAssist.Engine.Core;

namespace CursorAssist.Engine.Transforms;

/// <summary>
/// Velocity-adaptive 1st-order IIR low-pass (EMA) smoothing transform
/// with optional real-time frequency-adaptive MinAlpha.
///
/// DSP foundation:
///   EMA is a 1st-order IIR filter: H(z) = α / (1 - (1-α)z⁻¹)
///   Approximate -3dB cutoff: fc ≈ α·Fs / (2π)   where Fs = 60 Hz
///
///   α ≈ 0.25 → fc ≈ 2.4 Hz (strong tremor suppression)
///   α ≈ 0.63 → fc ≈ 6.0 Hz (moderate)
///   α ≈ 0.90 → fc ≈ 8.6 Hz (near pass-through)
///
/// Target band: suppress 4–12 Hz tremor, preserve &lt;3 Hz intentional motion.
///
/// Velocity-adaptive strategy (two-breakpoint):
///   |v| ≤ vLow  → alpha = minAlpha (full tremor suppression)
///   |v| ≥ vHigh → alpha = maxAlpha (near pass-through)
///   vLow &lt; |v| &lt; vHigh → SmoothStep interpolation
///
/// Frequency-adaptive mode (when SmoothingAdaptiveFrequencyEnabled = true):
///   Estimates tremor frequency in real-time via zero-crossing rate on
///   high-pass filtered deltas, then maps: α_min = Clamp(0.05236 × f_est, 0.20, 0.40).
///   Seeded from MotorProfile.TremorFrequencyHz on first tick.
///   All O(1) per tick, fully deterministic, no FFT.
///
/// Parameters from AssistiveConfig:
///   SmoothingStrength   — master control [0,1]; 0 = disabled
///   SmoothingMinAlpha   — alpha at/below vLow  (default 0.25 → fc ≈ 2.4 Hz)
///   SmoothingMaxAlpha   — alpha at/above vHigh (default 0.90 → fc ≈ 8.6 Hz)
///   SmoothingVelocityLow  — tremor velocity ceiling (default 0.5 vpx/tick)
///   SmoothingVelocityHigh — intentional motion floor (default 8.0 vpx/tick)
///   SmoothingAdaptiveFrequencyEnabled — enable real-time frequency adaptation
///
/// No target knowledge required. Universal base-layer filter.
/// </summary>
public sealed class SmoothingTransform : IInputTransform
{
    public string TransformId => "assist.smoothing.velocity-adaptive";

    private float _smoothX;
    private float _smoothY;
    private bool _initialized;

    // Frequency estimator (only active when adaptive mode enabled)
    private readonly TremorFrequencyEstimator _freqEstimator = new();
    private bool _freqEstimatorSeeded;

    public InputSample Apply(in InputSample input, TransformContext context)
    {
        var config = context.Config;
        float strength = config?.SmoothingStrength ?? 0f;

        if (strength <= 0f)
        {
            // No smoothing — just track raw position
            _smoothX = input.X;
            _smoothY = input.Y;
            _initialized = true;
            return input;
        }

        if (!_initialized)
        {
            _smoothX = input.X;
            _smoothY = input.Y;
            _initialized = true;
            return input;
        }

        // Compute velocity magnitude from per-tick deltas
        float velocity = MathF.Sqrt(input.Dx * input.Dx + input.Dy * input.Dy);

        // ── Determine minAlpha: adaptive or static ──
        float minAlpha;
        bool adaptiveEnabled = config!.SmoothingAdaptiveFrequencyEnabled;

        if (adaptiveEnabled)
        {
            // Seed estimator on first tick from profile
            if (!_freqEstimatorSeeded)
            {
                float seed = context.Profile?.TremorFrequencyHz ?? 0f;
                _freqEstimator.Initialize(seed);
                _freqEstimatorSeeded = true;
            }

            // Update with current tick's deltas
            _freqEstimator.Update(input.Dx, input.Dy, velocity);

            // Use dynamic minAlpha if valid, else fall back to config
            float dynamicMinAlpha = _freqEstimator.DynamicMinAlpha;
            minAlpha = dynamicMinAlpha >= 0f ? dynamicMinAlpha : config.SmoothingMinAlpha;
        }
        else
        {
            minAlpha = config.SmoothingMinAlpha;
        }

        float maxAlpha = config.SmoothingMaxAlpha;
        float vLow = config.SmoothingVelocityLow;
        float vHigh = config.SmoothingVelocityHigh;

        // Guard: ensure sane ranges
        if (minAlpha < 0.05f) minAlpha = 0.25f;
        if (maxAlpha <= minAlpha) maxAlpha = MathF.Max(minAlpha + 0.01f, 0.9f);
        if (vLow < 0f) vLow = 0.5f;
        if (vHigh <= vLow) vHigh = MathF.Max(vLow + 1f, 8f);

        // Two-breakpoint velocity-adaptive alpha:
        //   velocity ≤ vLow  → minAlpha (full tremor suppression)
        //   velocity ≥ vHigh → maxAlpha (near pass-through)
        //   between → SmoothStep interpolation
        float baseAlpha;
        if (velocity <= vLow)
        {
            baseAlpha = minAlpha;
        }
        else if (velocity >= vHigh)
        {
            baseAlpha = maxAlpha;
        }
        else
        {
            float t = (velocity - vLow) / (vHigh - vLow);
            float smooth = t * t * (3f - 2f * t); // Hermite smoothstep
            baseAlpha = minAlpha + (maxAlpha - minAlpha) * smooth;
        }

        // Apply master strength: strength=1 uses full adaptive range,
        // strength<1 biases alpha upward toward 1 (less smoothing overall)
        // alpha = Lerp(1.0, baseAlpha, strength)
        float alpha = 1f + strength * (baseAlpha - 1f);

        // Final clamp to prevent freeze or overshoot
        alpha = MathF.Max(0.05f, MathF.Min(1f, alpha));

        _smoothX += alpha * (input.X - _smoothX);
        _smoothY += alpha * (input.Y - _smoothY);

        return input with { X = _smoothX, Y = _smoothY };
    }

    public void Reset()
    {
        _smoothX = 0f;
        _smoothY = 0f;
        _initialized = false;
        _freqEstimator.Reset();
        _freqEstimatorSeeded = false;
    }

    /// <summary>
    /// Real-time tremor frequency estimator using high-pass filtered zero-crossing rate.
    /// O(1) per tick. Fixed-size circular buffer, no per-tick allocations.
    ///
    /// Algorithm:
    ///   1. High-pass pre-filter: delta − EMA_slow(delta) isolates tremor band
    ///   2. Zero-crossing counter over sliding 1-second window (60 ticks)
    ///   3. EMA on raw frequency estimate for temporal stability
    ///   4. Safeguards: frequency band [3, 15] Hz, velocity gate, amplitude floor
    ///
    /// Closed-form output:
    ///   α_min = Clamp(FreqToAlphaK × f_est, 0.20, 0.40)
    ///   where FreqToAlphaK = 2π × 0.5 / 60 ≈ 0.05236
    /// </summary>
    private sealed class TremorFrequencyEstimator
    {
        // ── Constants ──

        /// <summary>60 ticks = 1 second at 60 Hz. Provides 1 Hz frequency resolution.</summary>
        private const int WindowSize = 60;

        /// <summary>EMA alpha for slow baseline (~1 Hz cutoff at 60 Hz): α ≈ 2π·1/60 ≈ 0.105.</summary>
        private const float SlowEmaAlpha = 0.105f;

        /// <summary>EMA beta for frequency smoothing (slow adaptation, prevents jitter).</summary>
        private const float FreqEmaBeta = 0.1f;

        /// <summary>Minimum valid tremor frequency (Hz). Below this → intentional motion.</summary>
        private const float MinFreqHz = 3f;

        /// <summary>Maximum valid tremor frequency (Hz). Above this → noise/aliasing.</summary>
        private const float MaxFreqHz = 15f;

        /// <summary>Minimum high-pass magnitude to count as a real crossing (noise floor, vpx).</summary>
        private const float AmplitudeFloor = 0.15f;

        /// <summary>Velocity gate threshold (vpx/tick). Freeze adaptation above this.</summary>
        private const float VelocityGateThreshold = 4f;

        /// <summary>Consecutive ticks above gate to trigger freeze (~200ms at 60 Hz).</summary>
        private const int VelocityGateTickCount = 12;

        /// <summary>Closed-form mapping: 2π × 0.5 / 60 ≈ 0.05236.</summary>
        private const float FreqToAlphaK = 0.05236f;

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

        private bool _initialized;

        /// <summary>Current estimated tremor frequency in Hz.</summary>
        public float FrequencyHz => _freqEstimate;

        /// <summary>
        /// Dynamic minAlpha derived from frequency estimate.
        /// Returns ≥ 0 if valid, or −1 if no valid estimate (use static fallback).
        /// </summary>
        public float DynamicMinAlpha =>
            _freqEstimate >= MinFreqHz
                ? Math.Clamp(FreqToAlphaK * _freqEstimate, 0.20f, 0.40f)
                : -1f;

        /// <summary>Seed the estimator with an initial frequency from the motor profile.</summary>
        public void Initialize(float seedFrequencyHz)
        {
            _freqEstimate = seedFrequencyHz > 0f ? seedFrequencyHz : 0f;
            _bufferIndex = 0;
            _bufferCount = 0;
            _crossingCount = 0;
            _slowEmaX = 0f;
            _slowEmaY = 0f;
            _highVelocityTicks = 0;
            _initialized = false;
            Array.Clear(_signBuffer);
        }

        /// <summary>Update with this tick's raw deltas and velocity magnitude.</summary>
        public void Update(float dx, float dy, float velocity)
        {
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

        /// <summary>Reset all estimator state.</summary>
        public void Reset()
        {
            Initialize(0f);
        }
    }
}
