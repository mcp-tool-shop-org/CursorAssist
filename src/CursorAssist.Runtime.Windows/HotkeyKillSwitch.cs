using CursorAssist.Runtime.Core;
using CursorAssist.Runtime.Windows.Interop;

namespace CursorAssist.Runtime.Windows;

/// <summary>
/// Windows implementation of IKillSwitch using RegisterHotKey.
/// Hotkey: Ctrl+Shift+Pause (globally registered).
///
/// Architecture:
///   - Dedicated thread with a message-only window (HWND_MESSAGE parent).
///   - GetMessage pump blocks until WM_HOTKEY or WM_QUIT.
///   - Arm() → creates thread/window/registers hotkey.
///   - Disarm() → posts WM_QUIT, joins thread, destroys window.
///   - Dispose() → Disarm().
///
/// Thread-safe: Arm/Disarm can be called from any thread.
/// The Triggered event fires on the message pump thread.
/// </summary>
public sealed class HotkeyKillSwitch : IKillSwitch
{
    private const int HotkeyId = 0xCA55; // CursorAssist kill switch ID

    private Thread? _thread;
    private nint _hwnd;
    private volatile bool _armed;
    private bool _disposed;

    public bool IsArmed => _armed;

    public event Action? Triggered;

    public void Arm()
    {
        if (_armed || _disposed) return;

        _armed = true;
        _thread = new Thread(MessagePumpLoop)
        {
            Name = "CursorAssist.KillSwitch",
            IsBackground = true
        };
        _thread.Start();
    }

    public void Disarm()
    {
        if (!_armed) return;

        _armed = false;

        // Signal the message pump to exit
        if (_hwnd != 0)
        {
            NativeMethods.PostMessageW(_hwnd, NativeMethods.WM_QUIT, 0, 0);
        }

        _thread?.Join(timeout: TimeSpan.FromSeconds(2));
        _thread = null;
    }

    private void MessagePumpLoop()
    {
        // Create message-only window (no visible UI)
        _hwnd = NativeMethods.CreateWindowExW(
            0, 0, 0, 0,
            0, 0, 0, 0,
            NativeMethods.HWND_MESSAGE,
            0, 0, 0);

        if (_hwnd == 0)
        {
            _armed = false;
            return;
        }

        try
        {
            // Register Ctrl+Shift+Pause
            bool registered = NativeMethods.RegisterHotKey(
                _hwnd, HotkeyId,
                NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT,
                NativeMethods.VK_PAUSE);

            if (!registered)
            {
                _armed = false;
                return;
            }

            try
            {
                // Message pump — blocks until WM_QUIT
                while (_armed && NativeMethods.GetMessageW(out var msg, 0, 0, 0))
                {
                    if (msg.Message == NativeMethods.WM_HOTKEY)
                    {
                        Triggered?.Invoke();
                    }

                    NativeMethods.TranslateMessage(in msg);
                    NativeMethods.DispatchMessageW(in msg);
                }
            }
            finally
            {
                NativeMethods.UnregisterHotKey(_hwnd, HotkeyId);
            }
        }
        finally
        {
            NativeMethods.DestroyWindow(_hwnd);
            _hwnd = 0;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disarm();
    }
}
