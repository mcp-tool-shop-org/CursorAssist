namespace CursorAssist.Engine.Core;

/// <summary>
/// Describes a target for transforms and metrics. Immutable snapshot per tick.
/// Used by magnetism, benchmarking, and training modes.
/// </summary>
public readonly record struct TargetInfo(
    string Id,
    float CenterX,
    float CenterY,
    float Width,
    float Height)
{
    public float Left => CenterX - Width * 0.5f;
    public float Right => CenterX + Width * 0.5f;
    public float Top => CenterY - Height * 0.5f;
    public float Bottom => CenterY + Height * 0.5f;

    public bool Contains(float x, float y) =>
        x >= Left && x <= Right && y >= Top && y <= Bottom;

    public float DistanceTo(float x, float y)
    {
        float dx = MathF.Max(Left - x, MathF.Max(0, x - Right));
        float dy = MathF.Max(Top - y, MathF.Max(0, y - Bottom));
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    public float DistanceToCenter(float x, float y)
    {
        float dx = x - CenterX;
        float dy = y - CenterY;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
