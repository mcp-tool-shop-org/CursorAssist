using System.Runtime.InteropServices;
using CursorAssist.Runtime.Core;
using CursorAssist.Runtime.Windows.Interop;

namespace CursorAssist.Runtime.Windows;

/// <summary>
/// Injects assisted mouse movement via SendInput.
/// Tags injected events with AssistTag in dwExtraInfo for loop prevention.
/// Runs on a dedicated injection thread.
/// </summary>
public sealed class MouseInjector : IDisposable
{
    /// <summary>
    /// Magic value in dwExtraInfo that identifies injected events.
    /// Capture layer checks this to skip self-injected input.
    /// </summary>
    public const nuint AssistTag = 0xC0A55E57;

    private readonly EngineThread _engine;
    private Thread? _thread;
    private volatile bool _running;
    private bool _disposed;

    public MouseInjector(EngineThread engine)
    {
        _engine = engine;
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        _thread = new Thread(InjectLoop)
        {
            Name = "CursorAssist.InjectionThread",
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal
        };
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        _thread?.Join(timeout: TimeSpan.FromSeconds(2));
        _thread = null;
    }

    private void InjectLoop()
    {
        var inputs = new NativeMethods.INPUT[1];
        inputs[0].Type = NativeMethods.INPUT_MOUSE;

        while (_running)
        {
            if (_engine.InjectionQueue.TryDequeue(out var delta))
            {
                // Convert float deltas to integer mickeys
                int dx = (int)MathF.Round(delta.Dx);
                int dy = (int)MathF.Round(delta.Dy);

                if (dx != 0 || dy != 0)
                {
                    inputs[0].Mi = new NativeMethods.MOUSEINPUT
                    {
                        Dx = dx,
                        Dy = dy,
                        DwFlags = NativeMethods.MOUSEEVENTF_MOVE,
                        DwExtraInfo = AssistTag
                    };

                    NativeMethods.SendInput(1, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
                }
            }
            else
            {
                // No work — sleep briefly to avoid busy-wait
                Thread.Sleep(1);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
