namespace CursorAssist.Engine.Core;

/// <summary>
/// A pure, deterministic transform in the input pipeline.
/// Given (input, context, prior state) → (transformed input).
/// Transforms are composed in a chain: raw → preprocess → assist → output.
/// </summary>
public interface IInputTransform
{
    /// <summary>Unique identifier for this transform (for logging/replay).</summary>
    string TransformId { get; }

    /// <summary>
    /// Apply the transform. Must be deterministic given the same inputs.
    /// </summary>
    InputSample Apply(in InputSample input, TransformContext context);

    /// <summary>Reset any internal state (e.g., on session start).</summary>
    void Reset();
}
