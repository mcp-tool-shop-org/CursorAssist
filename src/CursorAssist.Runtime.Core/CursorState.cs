namespace CursorAssist.Runtime.Core;

/// <summary>
/// Engine-owned virtual cursor state. The OS is just a rendering surface.
/// Initialized from OS position once at Enable(), then maintained internally.
/// </summary>
public struct CursorState
{
    public float X { get; set; }
    public float Y { get; set; }
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public bool PrimaryDown { get; set; }
    public bool SecondaryDown { get; set; }

    public void Reset(float x, float y)
    {
        X = x;
        Y = y;
        VelocityX = 0f;
        VelocityY = 0f;
        PrimaryDown = false;
        SecondaryDown = false;
    }
}
