using CursorAssist.Canon.Schemas;
using CursorAssist.Engine.Core;

namespace CursorAssist.Engine.Metrics;

/// <summary>
/// Metrics sink that accumulates motor profiling data from training/profiling sessions.
/// Computes: tremor proxy, path efficiency, overshoot, time-to-target, click stability.
/// Exports a MotorProfile v1.
/// </summary>
public sealed class MotorProfileSink : IMetricsSink
{
    private readonly List<float> _highFreqDeltas = [];
    private readonly List<float> _pathEfficiencies = [];
    private readonly List<float> _timesToTarget = [];
    private readonly List<int> _overshoots = [];
    private readonly List<float> _overshootMagnitudes = [];
    private readonly List<float> _clickDisplacements = [];

    // Per-trial tracking
    private float _trialStartX, _trialStartY;
    private float _trialActualPath;
    private float _prevX, _prevY;
    private float _prevDx, _prevDy;
    private bool _hasPrev;
    private bool _inTrial;
    private long _trialStartTick;
    private int _trialOvershootCount;
    private float _trialOvershootMag;
    private bool _wasApproaching;

    // Click tracking
    private bool _wasDown;
    private float _clickStartX, _clickStartY;
    private float _clickMaxDisplacement;

    private int _sampleCount;

    public void BeginTrial(float startX, float startY)
    {
        _trialStartX = startX;
        _trialStartY = startY;
        _trialActualPath = 0f;
        _trialStartTick = -1;
        _trialOvershootCount = 0;
        _trialOvershootMag = 0f;
        _wasApproaching = false;
        _hasPrev = false;
        _inTrial = true;
    }

    public void EndTrial(long endTick, TargetInfo target)
    {
        if (!_inTrial) return;
        _inTrial = false;

        // Path efficiency
        float idealDist = MathF.Sqrt(
            (target.CenterX - _trialStartX) * (target.CenterX - _trialStartX) +
            (target.CenterY - _trialStartY) * (target.CenterY - _trialStartY));
        if (_trialActualPath > 0f)
            _pathEfficiencies.Add(idealDist / _trialActualPath);

        // Time to target
        if (_trialStartTick >= 0)
        {
            float ticks = endTick - _trialStartTick;
            _timesToTarget.Add(ticks / 60f); // fixed Hz
        }

        _overshoots.Add(_trialOvershootCount);
        if (_trialOvershootCount > 0)
            _overshootMagnitudes.Add(_trialOvershootMag / _trialOvershootCount);

        _sampleCount++;
    }

    public void RecordTick(long tick, in InputSample raw, in InputSample transformed, IReadOnlyList<TargetInfo> targets)
    {
        if (_inTrial && _trialStartTick < 0)
            _trialStartTick = tick;

        // High-frequency delta for tremor proxy
        float dx = raw.X - (_hasPrev ? _prevX : raw.X);
        float dy = raw.Y - (_hasPrev ? _prevY : raw.Y);

        if (_hasPrev)
        {
            // Second derivative (jerk) as tremor proxy
            float ddx = dx - _prevDx;
            float ddy = dy - _prevDy;
            _highFreqDeltas.Add(MathF.Sqrt(ddx * ddx + ddy * ddy));

            // Path accumulation
            if (_inTrial)
            {
                _trialActualPath += MathF.Sqrt(dx * dx + dy * dy);

                // Overshoot detection: was approaching target, now receding
                if (targets.Count > 0)
                {
                    var t = targets[0];
                    float distNow = t.DistanceToCenter(raw.X, raw.Y);
                    float distPrev = t.DistanceToCenter(_prevX, _prevY);
                    bool approaching = distNow < distPrev;

                    if (_wasApproaching && !approaching && !t.Contains(raw.X, raw.Y))
                    {
                        _trialOvershootCount++;
                        _trialOvershootMag += distPrev; // closest approach before reversal
                    }
                    _wasApproaching = approaching;
                }
            }
        }

        // Click stability: track displacement during click hold
        if (raw.PrimaryDown)
        {
            if (!_wasDown)
            {
                _clickStartX = raw.X;
                _clickStartY = raw.Y;
                _clickMaxDisplacement = 0f;
            }
            else
            {
                float cd = MathF.Sqrt(
                    (raw.X - _clickStartX) * (raw.X - _clickStartX) +
                    (raw.Y - _clickStartY) * (raw.Y - _clickStartY));
                if (cd > _clickMaxDisplacement)
                    _clickMaxDisplacement = cd;
            }
        }
        else if (_wasDown)
        {
            _clickDisplacements.Add(_clickMaxDisplacement);
        }

        _prevX = raw.X;
        _prevY = raw.Y;
        _prevDx = dx;
        _prevDy = dy;
        _hasPrev = true;
        _wasDown = raw.PrimaryDown;
    }

    public void RecordEvent(in EngineEvent engineEvent) { }

    public void Reset()
    {
        _highFreqDeltas.Clear();
        _pathEfficiencies.Clear();
        _timesToTarget.Clear();
        _overshoots.Clear();
        _overshootMagnitudes.Clear();
        _clickDisplacements.Clear();
        _sampleCount = 0;
        _hasPrev = false;
        _inTrial = false;
        _wasDown = false;
    }

    /// <summary>
    /// Export the accumulated data as a MotorProfile v1.
    /// </summary>
    public MotorProfile ExportProfile(string profileId)
    {
        return new MotorProfile
        {
            ProfileId = profileId,
            CreatedUtc = DateTimeOffset.UtcNow,
            TremorFrequencyHz = EstimateTremorFrequency(),
            TremorAmplitudeVpx = _highFreqDeltas.Count > 0 ? Rms(_highFreqDeltas) : 0f,
            PathEfficiency = _pathEfficiencies.Count > 0 ? _pathEfficiencies.Average() : 0f,
            OvershootRate = _overshoots.Count > 0 ? (float)_overshoots.Average() : 0f,
            OvershootMagnitudeVpx = _overshootMagnitudes.Count > 0 ? _overshootMagnitudes.Average() : 0f,
            MeanTimeToTargetS = _timesToTarget.Count > 0 ? _timesToTarget.Average() : 0f,
            StdDevTimeToTargetS = _timesToTarget.Count > 1 ? StdDev(_timesToTarget) : 0f,
            ClickStabilityVpx = _clickDisplacements.Count > 0 ? _clickDisplacements.Average() : 0f,
            SampleCount = _sampleCount
        };
    }

    private float EstimateTremorFrequency()
    {
        // Simple zero-crossing rate on high-freq deltas as frequency proxy
        if (_highFreqDeltas.Count < 3) return 0f;

        float mean = _highFreqDeltas.Average();
        int crossings = 0;
        for (int i = 1; i < _highFreqDeltas.Count; i++)
        {
            if ((_highFreqDeltas[i - 1] - mean) * (_highFreqDeltas[i] - mean) < 0)
                crossings++;
        }

        // Zero-crossing rate → freq: f ≈ (crossings / 2) / duration_seconds
        float durationS = _highFreqDeltas.Count / 60f;
        return durationS > 0f ? (crossings * 0.5f) / durationS : 0f;
    }

    private static float Rms(List<float> values)
    {
        float sumSq = 0f;
        foreach (var v in values) sumSq += v * v;
        return MathF.Sqrt(sumSq / values.Count);
    }

    private static float StdDev(List<float> values)
    {
        float mean = values.Average();
        float sumSq = 0f;
        foreach (var v in values) sumSq += (v - mean) * (v - mean);
        return MathF.Sqrt(sumSq / values.Count);
    }
}
