namespace MouseTrainer.MauiHost;

/// <summary>
/// Short-impulse screen shake with quadratic decay.
/// Writes offset values to RendererState each frame.
/// </summary>
public sealed class ScreenShake
{
    private float _amplitude;
    private float _remaining;
    private float _duration;
    private uint _seed;

    /// <summary>
    /// Trigger a shake impulse.
    /// </summary>
    /// <param name="amplitude">Max pixel displacement (virtual space).</param>
    /// <param name="duration">Total duration in seconds.</param>
    public void Trigger(float amplitude = 6f, float duration = 0.12f)
    {
        _amplitude = amplitude;
        _remaining = duration;
        _duration = duration;
        _seed = (uint)Environment.TickCount;
    }

    /// <summary>
    /// Advance shake and write offsets to state.
    /// </summary>
    public void Update(float dt, RendererState state)
    {
        if (_remaining <= 0f)
        {
            state.ShakeOffsetX = 0f;
            state.ShakeOffsetY = 0f;
            return;
        }

        _remaining -= dt;
        if (_remaining < 0f) _remaining = 0f;

        // Quadratic decay
        float t = _remaining / _duration;
        float currentAmp = _amplitude * t * t;

        // Pseudo-random direction per frame
        _seed = _seed * 1103515245 + 12345;
        float angle = (_seed & 0xFFFF) / 65535f * MathF.Tau;

        state.ShakeOffsetX = MathF.Cos(angle) * currentAmp;
        state.ShakeOffsetY = MathF.Sin(angle) * currentAmp;
    }

    public void Clear()
    {
        _remaining = 0f;
    }
}
