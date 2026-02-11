using System.Windows.Automation;
using CursorAssist.Engine.Core;
using CursorAssist.Runtime.Core;

namespace CursorAssist.Runtime.Windows;

/// <summary>
/// Lightweight UI Automation target provider.
/// Polls focused element + invocable siblings at ~3 Hz on a background thread.
/// Converts bounding rects to TargetInfo for the magnetism transform.
///
/// Design constraints:
///   - Max 20 targets per poll (prevents degenerate UI trees)
///   - All COM calls wrapped in try-catch (UIA can throw on any call)
///   - Back-off on 5+ consecutive failures (prevents spin on broken UIA)
///   - Volatile array swap for lock-free reads from engine thread
///   - No per-tick allocations on the read path
///
/// Poll strategy:
///   1. AutomationElement.FocusedElement → walk to parent
///   2. Enumerate sibling children matching InvokePattern or TogglePattern
///   3. Extract BoundingRectangle → TargetInfo
/// </summary>
public sealed class UIAutomationTargetProvider : ITargetProvider
{
    private const int MaxTargets = 20;
    private const int PollIntervalMs = 333; // ~3 Hz
    private const int MaxConsecutiveFailures = 5;
    private const int BackoffMs = 2000; // Back-off on sustained failures

    private volatile TargetInfo[] _targets = [];
    private Thread? _thread;
    private volatile bool _running;
    private bool _disposed;

    public IReadOnlyList<TargetInfo> CurrentTargets => _targets;

    public void Start()
    {
        if (_running) return;
        _running = true;
        _thread = new Thread(PollLoop)
        {
            Name = "CursorAssist.UIAutomation",
            IsBackground = true
        };
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        _thread?.Join(timeout: TimeSpan.FromSeconds(2));
        _thread = null;
    }

    private void PollLoop()
    {
        int consecutiveFailures = 0;

        while (_running)
        {
            try
            {
                var targets = PollTargets();
                _targets = targets;
                consecutiveFailures = 0;
            }
            catch
            {
                // UIA COM calls can throw at any time — swallow and track.
                consecutiveFailures++;

                if (consecutiveFailures >= MaxConsecutiveFailures)
                {
                    // Back off — UIA is likely in a bad state
                    _targets = [];
                    Thread.Sleep(BackoffMs);
                    consecutiveFailures = 0;
                    continue;
                }
            }

            Thread.Sleep(PollIntervalMs);
        }
    }

    private static TargetInfo[] PollTargets()
    {
        var focused = AutomationElement.FocusedElement;
        if (focused == null) return [];

        // Walk to parent to enumerate siblings (the focused element's container)
        var walker = TreeWalker.ControlViewWalker;
        var parent = walker.GetParent(focused);
        if (parent == null || parent == AutomationElement.RootElement)
        {
            // Focused element is top-level — try to enumerate its children instead
            return EnumerateInvocableChildren(focused);
        }

        return EnumerateInvocableChildren(parent);
    }

    private static TargetInfo[] EnumerateInvocableChildren(AutomationElement container)
    {
        // Find children that support InvokePattern or TogglePattern
        var condition = new OrCondition(
            new PropertyCondition(AutomationElement.IsInvokePatternAvailableProperty, true),
            new PropertyCondition(AutomationElement.IsTogglePatternAvailableProperty, true));

        AutomationElementCollection? children;
        try
        {
            children = container.FindAll(TreeScope.Children, condition);
        }
        catch
        {
            return [];
        }

        if (children == null || children.Count == 0) return [];

        int count = Math.Min(children.Count, MaxTargets);
        var results = new List<TargetInfo>(count);

        for (int i = 0; i < count; i++)
        {
            try
            {
                var element = children[i];
                var rect = element.Current.BoundingRectangle;

                // Skip elements with invalid/empty bounds
                if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0)
                    continue;

                // Skip off-screen elements
                if (rect.X < -10000 || rect.Y < -10000)
                    continue;

                float cx = (float)(rect.X + rect.Width / 2.0);
                float cy = (float)(rect.Y + rect.Height / 2.0);
                float w = (float)rect.Width;
                float h = (float)rect.Height;

                string id = element.Current.AutomationId;
                if (string.IsNullOrEmpty(id))
                    id = element.Current.Name ?? $"uia-{i}";

                results.Add(new TargetInfo(id, cx, cy, w, h));
            }
            catch
            {
                // Individual element access can fail — skip
            }
        }

        return results.ToArray();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
