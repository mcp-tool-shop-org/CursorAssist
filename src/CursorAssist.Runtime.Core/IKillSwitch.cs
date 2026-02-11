namespace CursorAssist.Runtime.Core;

/// <summary>
/// Emergency stop interface. Arm() activates the hotkey listener.
/// When triggered, the Triggered event fires on an unspecified thread.
/// The host is responsible for wiring Triggered to engine shutdown
/// (e.g., EngineThread.EmergencyStop()).
///
/// Implementations:
///   - HotkeyKillSwitch (Windows): Ctrl+Shift+Pause via RegisterHotKey
/// </summary>
public interface IKillSwitch : IDisposable
{
    /// <summary>Activate the kill switch hotkey listener.</summary>
    void Arm();

    /// <summary>Deactivate the kill switch hotkey listener.</summary>
    void Disarm();

    /// <summary>Whether the kill switch is currently armed.</summary>
    bool IsArmed { get; }

    /// <summary>Fires when the kill switch hotkey is pressed.</summary>
    event Action? Triggered;
}
