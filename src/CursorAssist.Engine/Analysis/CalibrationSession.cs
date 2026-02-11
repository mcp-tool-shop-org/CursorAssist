namespace CursorAssist.Engine.Analysis;

/// <summary>
/// Records input ticks for a calibration period and produces a CalibrationResult.
/// Default duration: 300 ticks (5 seconds at 60 Hz).
///
/// Usage:
///   var session = new CalibrationSession();
///   while (!session.IsComplete)
///       session.RecordTick(dx, dy);
///   var result = session.GetResult();
///   var profile = result.ToMotorProfile("user-calibrated");
///   var config = ProfileToConfigMapper.Map(profile);
/// </summary>
public sealed class CalibrationSession
{
    private readonly TremorAnalyzer _analyzer = new();
    private readonly int _targetTicks;
    private int _tickCount;

    /// <summary>
    /// Create a calibration session.
    /// </summary>
    /// <param name="durationTicks">Number of ticks to record. Default 300 (5s at 60 Hz).</param>
    public CalibrationSession(int durationTicks = 300)
    {
        _targetTicks = durationTicks;
        _analyzer.Initialize(0f);
    }

    /// <summary>Whether enough ticks have been recorded.</summary>
    public bool IsComplete => _tickCount >= _targetTicks;

    /// <summary>Number of ticks recorded so far.</summary>
    public int TickCount => _tickCount;

    /// <summary>Target number of ticks.</summary>
    public int TargetTicks => _targetTicks;

    /// <summary>
    /// Record a single tick of input.
    /// </summary>
    public void RecordTick(float dx, float dy)
    {
        if (IsComplete) return;

        float velocity = MathF.Sqrt(dx * dx + dy * dy);
        _analyzer.Update(dx, dy, velocity);
        _tickCount++;
    }

    /// <summary>
    /// Get the calibration result. Can be called before IsComplete for intermediate results.
    /// </summary>
    public CalibrationResult GetResult()
    {
        float freq = _analyzer.FrequencyHz;
        float amp = _analyzer.AmplitudeVpx;

        // Confidence: based on sample count and whether a valid frequency was detected
        float tickRatio = Math.Clamp((float)_tickCount / _targetTicks, 0f, 1f);
        bool hasFrequency = freq >= TremorAnalyzer.MinFreqHz;

        // High confidence requires both enough samples AND a detected frequency
        float confidence = hasFrequency
            ? tickRatio * 0.8f + 0.2f  // [0.2, 1.0] when frequency detected
            : tickRatio * 0.3f;         // [0.0, 0.3] without frequency

        return new CalibrationResult(freq, amp, confidence, _tickCount);
    }
}
