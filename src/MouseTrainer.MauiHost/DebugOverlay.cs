namespace MouseTrainer.MauiHost;

/// <summary>
/// Mutable state snapshot consumed by the debug overlay drawable.
/// Updated by the host each frame; read by the draw method.
/// </summary>
public sealed class DebugOverlayState
{
    // Pointer
    public float CursorX;
    public float CursorY;
    public bool PrimaryDown;

    // Mapping params (virtual → device)
    public float OffsetX;
    public float OffsetY;
    public float Scale;

    // Sim
    public long Tick;
    public int Score;
    public int Combo;

    // Gate preview (optional — populated when ISimDebugOverlay is available)
    public bool HasGate;
    public float GateWallX;
    public float GateCenterY;
    public float GateApertureHeight;
    public int GateIndex;
    public float ScrollX;
    public bool LevelComplete;
}

/// <summary>
/// Debug overlay drawable: playfield bounds, cursor dot, gate preview, HUD.
/// Drawn on a GraphicsView layered over the game surface.
/// </summary>
public sealed class DebugOverlayDrawable : IDrawable
{
    private const float VirtualW = 1920f;
    private const float VirtualH = 1080f;

    private readonly DebugOverlayState _s;

    public DebugOverlayDrawable(DebugOverlayState state) => _s = state;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var scale = _s.Scale;
        if (scale <= 0.0001f)
            scale = MathF.Min(dirtyRect.Width / VirtualW, dirtyRect.Height / VirtualH);

        var contentW = VirtualW * scale;
        var contentH = VirtualH * scale;
        var offsetX = _s.OffsetX;
        var offsetY = _s.OffsetY;

        if (offsetX == 0f && offsetY == 0f && scale > 0f)
        {
            offsetX = (dirtyRect.Width - contentW) * 0.5f;
            offsetY = (dirtyRect.Height - contentH) * 0.5f;
        }

        // --- Playfield bounds ---
        canvas.StrokeSize = 2;
        canvas.StrokeColor = Colors.White;
        canvas.StrokeDashPattern = new float[] { 4, 4 };
        canvas.DrawRectangle(offsetX, offsetY, contentW, contentH);
        canvas.StrokeDashPattern = null;

        // --- Gate preview ---
        if (_s.HasGate)
        {
            // Gate X relative to scroll (world → viewport)
            var gateScreenX = offsetX + (_s.GateWallX - _s.ScrollX) * scale;

            // Only draw if the gate is within view
            if (gateScreenX >= offsetX - 20 && gateScreenX <= offsetX + contentW + 20)
            {
                var gapHalf = _s.GateApertureHeight * 0.5f * scale;
                var gapCenterScreenY = offsetY + _s.GateCenterY * scale;

                // Top wall (above aperture)
                canvas.StrokeSize = 3;
                canvas.StrokeColor = Colors.Yellow;
                canvas.DrawLine(gateScreenX, offsetY, gateScreenX, gapCenterScreenY - gapHalf);

                // Bottom wall (below aperture)
                canvas.DrawLine(gateScreenX, gapCenterScreenY + gapHalf, gateScreenX, offsetY + contentH);

                // Aperture edges (horizontal ticks)
                canvas.StrokeSize = 1;
                canvas.StrokeColor = Colors.LimeGreen;
                canvas.DrawLine(gateScreenX - 8, gapCenterScreenY - gapHalf, gateScreenX + 8, gapCenterScreenY - gapHalf);
                canvas.DrawLine(gateScreenX - 8, gapCenterScreenY + gapHalf, gateScreenX + 8, gapCenterScreenY + gapHalf);

                // Gate label
                canvas.FontSize = 11;
                canvas.FontColor = Colors.Yellow;
                canvas.DrawString($"gate {_s.GateIndex}", gateScreenX + 6, offsetY + 16, HorizontalAlignment.Left);
            }
        }

        // --- Cursor dot ---
        var cx = offsetX + _s.CursorX * scale;
        var cy = offsetY + _s.CursorY * scale;

        canvas.FillColor = _s.PrimaryDown ? Colors.LimeGreen : Colors.Cyan;
        canvas.FillCircle(cx, cy, 6);

        // Crosshair
        canvas.StrokeSize = 1;
        canvas.StrokeColor = Colors.White;
        canvas.Alpha = 0.5f;
        canvas.DrawLine(cx - 12, cy, cx + 12, cy);
        canvas.DrawLine(cx, cy - 12, cx, cy + 12);
        canvas.Alpha = 1f;

        // --- HUD ---
        canvas.FontSize = 12;
        canvas.FontColor = Colors.White;

        var hudY = offsetY + contentH - 20;
        canvas.DrawString(
            $"tick={_s.Tick}  Y={_s.CursorY:0}  score={_s.Score}  combo={_s.Combo}",
            offsetX + 8, hudY, HorizontalAlignment.Left);

        if (_s.LevelComplete)
        {
            canvas.FontSize = 24;
            canvas.FontColor = Colors.Gold;
            canvas.DrawString(
                $"LEVEL COMPLETE — score {_s.Score}",
                dirtyRect.Width * 0.5f, dirtyRect.Height * 0.5f, HorizontalAlignment.Center);
        }
    }
}
