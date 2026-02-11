using System.Diagnostics;
using System.Runtime.InteropServices;
using CursorAssist.Runtime.Core;
using CursorAssist.Runtime.Windows.Interop;

namespace CursorAssist.Runtime.Windows;

/// <summary>
/// Captures mouse input via WH_MOUSE_LL hook.
/// Normalizes into RawInputEvent and pushes to EngineThread's input queue.
/// Filters out self-injected events (AssistTag in dwExtraInfo).
/// </summary>
public sealed class MouseCapture : IDisposable
{
    private readonly EngineThread _engine;
    private nint _hookId;
    private NativeMethods.LowLevelMouseProc? _proc;
    private bool _disposed;

    private int _lastX, _lastY;
    private bool _hasLast;
    private bool _primaryDown;
    private bool _secondaryDown;

    public MouseCapture(EngineThread engine)
    {
        _engine = engine;
    }

    public void Start()
    {
        _proc = HookCallback;
        var moduleHandle = NativeMethods.GetModuleHandleW(null);
        _hookId = NativeMethods.SetWindowsHookExW(
            NativeMethods.WH_MOUSE_LL,
            _proc,
            moduleHandle,
            0);

        if (_hookId == 0)
            throw new InvalidOperationException($"Failed to set mouse hook. Error: {Marshal.GetLastWin32Error()}");

        // Initialize with current cursor position
        if (NativeMethods.GetCursorPos(out var pt))
        {
            _lastX = pt.X;
            _lastY = pt.Y;
            _hasLast = true;
        }
    }

    public void Stop()
    {
        if (_hookId != 0)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = 0;
        }
        _proc = null;
    }

    private nint HookCallback(int nCode, nuint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);

            // Loop prevention layer 1: check AssistTag
            if (hookStruct.DwExtraInfo == MouseInjector.AssistTag)
                return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);

            int msg = (int)wParam;

            // Track button state
            switch (msg)
            {
                case NativeMethods.WM_LBUTTONDOWN:
                    _primaryDown = true;
                    break;
                case NativeMethods.WM_LBUTTONUP:
                    _primaryDown = false;
                    break;
                case NativeMethods.WM_RBUTTONDOWN:
                    _secondaryDown = true;
                    break;
                case NativeMethods.WM_RBUTTONUP:
                    _secondaryDown = false;
                    break;
            }

            // Compute deltas
            float dx = 0f, dy = 0f;
            if (_hasLast)
            {
                dx = hookStruct.Pt.X - _lastX;
                dy = hookStruct.Pt.Y - _lastY;
            }

            // Loop prevention layer 2: check against recent injections
            if (_engine.WasRecentlyInjected(dx, dy))
            {
                _lastX = hookStruct.Pt.X;
                _lastY = hookStruct.Pt.Y;
                _hasLast = true;
                return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            var rawEvent = new RawInputEvent(
                dx, dy,
                _primaryDown, _secondaryDown,
                Stopwatch.GetTimestamp());

            _engine.InputQueue.Enqueue(rawEvent);

            _lastX = hookStruct.Pt.X;
            _lastY = hookStruct.Pt.Y;
            _hasLast = true;
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
