namespace CursorAssist.Engine.Core;

/// <summary>
/// Ordered chain of input transforms. Applied sequentially: raw → t1 → t2 → ... → final.
/// Pure given the same input sequence and context.
/// </summary>
public sealed class TransformPipeline
{
    private readonly List<IInputTransform> _transforms = [];

    public IReadOnlyList<IInputTransform> Transforms => _transforms;

    public TransformPipeline() { }

    public TransformPipeline(IEnumerable<IInputTransform> transforms)
    {
        _transforms.AddRange(transforms);
    }

    public TransformPipeline Add(IInputTransform transform)
    {
        _transforms.Add(transform);
        return this;
    }

    /// <summary>
    /// Apply all transforms in order. Returns the final output sample.
    /// </summary>
    public InputSample Apply(in InputSample raw, TransformContext context)
    {
        var current = raw;
        foreach (var transform in _transforms)
        {
            current = transform.Apply(in current, context);
        }
        return current;
    }

    public void Reset()
    {
        foreach (var transform in _transforms)
        {
            transform.Reset();
        }
    }
}
