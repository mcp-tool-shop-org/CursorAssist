namespace MouseTrainer.MauiHost;

/// <summary>
/// Fixed-capacity ring buffer storing recent cursor positions with timestamps.
/// Pushed each frame by the host; read by the renderer for trail drawing.
/// </summary>
public sealed class TrailBuffer
{
    public readonly struct TrailPoint
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Time;

        public TrailPoint(float x, float y, float time)
        {
            X = x;
            Y = y;
            Time = time;
        }
    }

    private readonly TrailPoint[] _points;
    private int _head;
    private int _count;

    public int Count => _count;
    public int Capacity { get; }

    public TrailBuffer(int capacity = 16)
    {
        Capacity = capacity;
        _points = new TrailPoint[capacity];
    }

    /// <summary>
    /// Push a new point. Overwrites oldest if full.
    /// </summary>
    public void Push(float x, float y, float time)
    {
        _points[_head] = new TrailPoint(x, y, time);
        _head = (_head + 1) % Capacity;
        if (_count < Capacity) _count++;
    }

    /// <summary>
    /// Get point by age index. 0 = oldest still in buffer, Count-1 = newest.
    /// </summary>
    public TrailPoint GetByAge(int ageIndex)
    {
        int start = (_head - _count + Capacity) % Capacity;
        int idx = (start + ageIndex) % Capacity;
        return _points[idx];
    }

    public void Clear()
    {
        _head = 0;
        _count = 0;
    }
}
