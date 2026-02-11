using CursorAssist.Engine.Core;

namespace CursorAssist.Engine.Metrics;

/// <summary>
/// Metrics sink that computes Fitts' Law and accessibility metrics
/// for benchmark trials. Tracks per-target and aggregate stats.
/// </summary>
public sealed class BenchmarkMetricsSink : IMetricsSink
{
    private readonly List<TrialRecord> _trials = [];
    private TrialRecord? _currentTrial;
    private float _lastX, _lastY;
    private bool _hasLast;

    public IReadOnlyList<TrialRecord> Trials => _trials;

    public void BeginTrial(TargetInfo target, float startX, float startY)
    {
        _currentTrial = new TrialRecord
        {
            TargetId = target.Id,
            Target = target,
            StartX = startX,
            StartY = startY,
            StartTick = -1
        };
        _lastX = startX;
        _lastY = startY;
        _hasLast = true;
    }

    public void EndTrial(bool hit, long endTick)
    {
        if (_currentTrial is null) return;
        _currentTrial.Hit = hit;
        _currentTrial.EndTick = endTick;
        _trials.Add(_currentTrial);
        _currentTrial = null;
    }

    public void RecordTick(long tick, in InputSample raw, in InputSample transformed, IReadOnlyList<TargetInfo> targets)
    {
        if (_currentTrial is null) return;

        if (_currentTrial.StartTick < 0)
            _currentTrial.StartTick = tick;

        // Accumulate actual path distance
        if (_hasLast)
        {
            float dx = transformed.X - _lastX;
            float dy = transformed.Y - _lastY;
            _currentTrial.ActualPathLength += MathF.Sqrt(dx * dx + dy * dy);
        }

        _lastX = transformed.X;
        _lastY = transformed.Y;
        _hasLast = true;

        // Record endpoint for effective width calculation
        if (_currentTrial.Target.Contains(transformed.X, transformed.Y) && transformed.PrimaryDown)
        {
            _currentTrial.HitX = transformed.X;
            _currentTrial.HitY = transformed.Y;
        }
    }

    public void RecordEvent(in EngineEvent engineEvent) { }

    public void Reset()
    {
        _trials.Clear();
        _currentTrial = null;
        _hasLast = false;
    }

    /// <summary>
    /// Compute aggregate Fitts' Law metrics from completed trials.
    /// </summary>
    public BenchmarkSummary ComputeSummary()
    {
        if (_trials.Count == 0)
            return BenchmarkSummary.Empty;

        float totalTimeS = 0f;
        float totalPathEff = 0f;
        float totalId = 0f;
        int hits = 0;
        int total = _trials.Count;
        var hitEndpoints = new List<(float dx, float dy)>();

        foreach (var trial in _trials)
        {
            float idealDist = MathF.Sqrt(
                (trial.Target.CenterX - trial.StartX) * (trial.Target.CenterX - trial.StartX) +
                (trial.Target.CenterY - trial.StartY) * (trial.Target.CenterY - trial.StartY));

            float durationTicks = trial.EndTick - trial.StartTick;
            float durationS = durationTicks / 60f; // fixed Hz
            totalTimeS += durationS;

            if (trial.ActualPathLength > 0f)
                totalPathEff += idealDist / trial.ActualPathLength;

            // Fitts' index of difficulty: ID = log2(D/W + 1)
            float targetW = MathF.Min(trial.Target.Width, trial.Target.Height);
            if (targetW > 0f && idealDist > 0f)
                totalId += MathF.Log2(idealDist / targetW + 1f);

            if (trial.Hit)
            {
                hits++;
                hitEndpoints.Add((trial.HitX - trial.Target.CenterX, trial.HitY - trial.Target.CenterY));
            }
        }

        // Effective width: We = 4.133 * SDx (Shannon formulation)
        float effectiveWidth = 0f;
        if (hitEndpoints.Count > 1)
        {
            float meanDx = hitEndpoints.Average(p => p.dx);
            float meanDy = hitEndpoints.Average(p => p.dy);
            float variance = hitEndpoints.Average(p =>
                (p.dx - meanDx) * (p.dx - meanDx) + (p.dy - meanDy) * (p.dy - meanDy));
            float sd = MathF.Sqrt(variance);
            effectiveWidth = 4.133f * sd;
        }

        float meanTimeS = totalTimeS / total;
        float meanId = totalId / total;
        float throughput = meanTimeS > 0f ? meanId / meanTimeS : 0f;

        return new BenchmarkSummary
        {
            TrialCount = total,
            HitCount = hits,
            ErrorRate = 1f - (float)hits / total,
            MeanTimeToTargetS = meanTimeS,
            MeanPathEfficiency = totalPathEff / total,
            MeanIndexOfDifficulty = meanId,
            MeanEffectiveWidthVpx = effectiveWidth,
            ThroughputBitsPerS = throughput
        };
    }

    public sealed class TrialRecord
    {
        public required string TargetId { get; init; }
        public required TargetInfo Target { get; init; }
        public required float StartX { get; init; }
        public required float StartY { get; init; }
        public long StartTick { get; set; }
        public long EndTick { get; set; }
        public bool Hit { get; set; }
        public float HitX { get; set; }
        public float HitY { get; set; }
        public float ActualPathLength { get; set; }
    }
}

public sealed record BenchmarkSummary
{
    public static readonly BenchmarkSummary Empty = new()
    {
        TrialCount = 0,
        HitCount = 0,
        ErrorRate = 0f,
        MeanTimeToTargetS = 0f,
        MeanPathEfficiency = 0f,
        MeanIndexOfDifficulty = 0f,
        MeanEffectiveWidthVpx = 0f,
        ThroughputBitsPerS = 0f
    };

    public required int TrialCount { get; init; }
    public required int HitCount { get; init; }
    public required float ErrorRate { get; init; }
    public required float MeanTimeToTargetS { get; init; }
    public required float MeanPathEfficiency { get; init; }
    public required float MeanIndexOfDifficulty { get; init; }
    public required float MeanEffectiveWidthVpx { get; init; }
    public required float ThroughputBitsPerS { get; init; }
}
