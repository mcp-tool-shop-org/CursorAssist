using CursorAssist.Engine.Core;

namespace CursorAssist.Runtime.Core;

/// <summary>
/// Provides UI targets for the magnetism transform.
/// Implementations poll platform accessibility APIs (e.g., UI Automation)
/// and expose a snapshot of current targets.
///
/// Threading contract:
///   - CurrentTargets is read by the engine thread at frame boundary.
///   - Implementation updates via volatile array swap (zero allocation on read).
///   - Start()/Stop() called from the main thread.
/// </summary>
public interface ITargetProvider : IDisposable
{
    /// <summary>
    /// Current snapshot of UI targets. Updated asynchronously by the provider.
    /// Never null — returns empty array when no targets are available.
    /// </summary>
    IReadOnlyList<TargetInfo> CurrentTargets { get; }

    /// <summary>Begin polling for targets.</summary>
    void Start();

    /// <summary>Cease polling for targets.</summary>
#pragma warning disable CA1716 // Identifiers should not match keywords
    void Stop();
#pragma warning restore CA1716
}
