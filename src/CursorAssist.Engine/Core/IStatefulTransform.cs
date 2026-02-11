namespace CursorAssist.Engine.Core;

/// <summary>
/// Marker interface for transforms that hold internal state across ticks.
/// <see cref="IInputTransform.Reset"/> must return all mutable state to a
/// deterministic initial condition such that running the same input stream
/// after a reset produces identical output (same FNV-1a hash).
///
/// Stateless transforms (e.g., PhaseCompensationTransform) implement
/// <see cref="IInputTransform"/> directly without this marker.
/// </summary>
public interface IStatefulTransform : IInputTransform { }
