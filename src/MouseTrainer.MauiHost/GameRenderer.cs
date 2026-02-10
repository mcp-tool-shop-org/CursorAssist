namespace MouseTrainer.MauiHost;

// ═══════════════════════════════════════════════════════════════
//  Sandbox phase (replaces SessionState for host-only use)
// ═══════════════════════════════════════════════════════════════

public enum SandboxPhase { Idle, Running }

// ═══════════════════════════════════════════════════════════════
//  RendererState — mutable snapshot consumed by the renderer
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Mutable state snapshot consumed by the renderer. Updated by the host each frame.
/// Gate-specific fields removed — this is a clean sandbox canvas.
/// </summary>
public sealed class RendererState
{
    // ── Pointer ──────────────────────────────────────────
    public float CursorX;
    public float CursorY;
    public bool PrimaryDown;

    // ── Mapping (virtual → device) ───────────────────────
    public float OffsetX;
    public float OffsetY;
    public float Scale;

    // ── Sim time ─────────────────────────────────────────
    public long Tick;
    public float SimTime;
    public float Alpha;

    // ── Sandbox phase ────────────────────────────────────
    public SandboxPhase SessionPhase;

    // ── Cursor trail ─────────────────────────────────────
    public TrailBuffer? Trail;

    // ── Particles ────────────────────────────────────────
    public ParticleSystem? Particles;

    // ── Screen shake ─────────────────────────────────────
    public float ShakeOffsetX;
    public float ShakeOffsetY;
}

// ═══════════════════════════════════════════════════════════════
//  GameRenderer : IDrawable
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Neon-minimal sandbox renderer. Draws background, grid, cursor trail, and cursor.
/// Ready to layer any future game mode rendering on top.
/// </summary>
public sealed class GameRenderer : IDrawable
{
    private const float VW = 1920f;
    private const float VH = 1080f;

    private readonly RendererState _s;

    public GameRenderer(RendererState state) => _s = state;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        ComputeTransform(dirtyRect, out float scale, out float ox, out float oy,
                         out float cw, out float ch);

        if (_s.SessionPhase == SandboxPhase.Idle)
        {
            DrawIdleScreen(canvas, dirtyRect, ox, oy, cw, ch);
            return;
        }

        // ── Running state ────────────────────────────────
        float shakeOx = ox + _s.ShakeOffsetX;
        float shakeOy = oy + _s.ShakeOffsetY;

        DrawBackground(canvas, shakeOx, shakeOy, cw, ch);
        DrawScanlines(canvas, shakeOx, shakeOy, cw, ch);
        DrawCorridorBounds(canvas, shakeOx, shakeOy, cw, ch);
        DrawParallaxLayers(canvas, shakeOx, shakeOy, cw, ch, scale);
        _s.Particles?.Draw(canvas, shakeOx, shakeOy, scale);
        DrawCursorTrail(canvas, shakeOx, shakeOy, scale);
        DrawCursor(canvas, shakeOx, shakeOy, scale);
        DrawHud(canvas, ox, oy, cw, ch);
    }

    // ══════════════════════════════════════════════════════
    //  Transform
    // ══════════════════════════════════════════════════════

    private void ComputeTransform(RectF rect, out float scale,
        out float ox, out float oy, out float cw, out float ch)
    {
        scale = _s.Scale;
        if (scale <= 0.0001f)
            scale = MathF.Min(rect.Width / VW, rect.Height / VH);

        cw = VW * scale;
        ch = VH * scale;
        ox = _s.OffsetX;
        oy = _s.OffsetY;

        if (ox == 0f && oy == 0f && scale > 0f)
        {
            ox = (rect.Width - cw) * 0.5f;
            oy = (rect.Height - ch) * 0.5f;
        }
    }

    // ══════════════════════════════════════════════════════
    //  Idle screen
    // ══════════════════════════════════════════════════════

    private static void DrawIdleScreen(ICanvas canvas, RectF rect,
        float ox, float oy, float cw, float ch)
    {
        // Background
        var gradient = new LinearGradientPaint(
            new PaintGradientStop[]
            {
                new(0f, NeonPalette.BgDeep),
                new(1f, NeonPalette.BgMid)
            },
            new Point(0, 0), new Point(0, 1));
        canvas.SetFillPaint(gradient, new RectF(ox, oy, cw, ch));
        canvas.FillRectangle(ox, oy, cw, ch);

        // Title
        canvas.FontSize = 22;
        canvas.FontColor = NeonPalette.Cyan;
        canvas.DrawString("MOUSE TRAINER",
            rect.Width * 0.5f, rect.Height * 0.38f, HorizontalAlignment.Center);

        // Subtitle
        canvas.FontSize = 12;
        canvas.FontColor = NeonPalette.TextDim;
        canvas.DrawString("SANDBOX MODE",
            rect.Width * 0.5f, rect.Height * 0.45f, HorizontalAlignment.Center);

        // Pulsing prompt
        float pulse = 0.4f + 0.6f * MathF.Abs(MathF.Sin((float)Environment.TickCount64 * 0.003f));
        canvas.FontSize = 18;
        canvas.FontColor = NeonPalette.Lime.WithAlpha(pulse);
        canvas.DrawString("PRESS START",
            rect.Width * 0.5f, rect.Height * 0.58f, HorizontalAlignment.Center);
    }

    // ══════════════════════════════════════════════════════
    //  Background
    // ══════════════════════════════════════════════════════

    private static void DrawBackground(ICanvas canvas, float ox, float oy, float cw, float ch)
    {
        var gradient = new LinearGradientPaint(
            new PaintGradientStop[]
            {
                new(0f, NeonPalette.BgDeep),
                new(1f, NeonPalette.BgMid)
            },
            new Point(0, 0), new Point(0, 1));
        canvas.SetFillPaint(gradient, new RectF(ox, oy, cw, ch));
        canvas.FillRectangle(ox, oy, cw, ch);
    }

    // ══════════════════════════════════════════════════════
    //  Scanlines
    // ══════════════════════════════════════════════════════

    private static void DrawScanlines(ICanvas canvas, float ox, float oy, float cw, float ch)
    {
        canvas.StrokeSize = 1;
        canvas.StrokeColor = Colors.Black.WithAlpha(0.06f);

        for (float y = oy; y < oy + ch; y += 4f)
            canvas.DrawLine(ox, y, ox + cw, y);
    }

    // ══════════════════════════════════════════════════════
    //  Corridor bounds
    // ══════════════════════════════════════════════════════

    private void DrawCorridorBounds(ICanvas canvas, float ox, float oy, float cw, float ch)
    {
        float pulse = 0.15f + 0.15f * MathF.Sin(_s.SimTime * 1.5f);

        // Sharp edge
        canvas.StrokeSize = 2;
        canvas.StrokeColor = NeonPalette.Cyan.WithAlpha(pulse);
        canvas.DrawLine(ox, oy, ox + cw, oy);
        canvas.DrawLine(ox, oy + ch, ox + cw, oy + ch);

        // Outer glow
        canvas.StrokeSize = 6;
        canvas.StrokeColor = NeonPalette.CyanGlow.WithAlpha(pulse * 0.4f);
        canvas.DrawLine(ox, oy, ox + cw, oy);
        canvas.DrawLine(ox, oy + ch, ox + cw, oy + ch);
    }

    // ══════════════════════════════════════════════════════
    //  Parallax grids
    // ══════════════════════════════════════════════════════

    private static void DrawParallaxLayers(ICanvas canvas, float ox, float oy,
        float cw, float ch, float scale)
    {
        DrawGridLayer(canvas, ox, oy, cw, ch, scale,
            spacing: 120f, color: NeonPalette.TextMuted.WithAlpha(0.06f), strokeWidth: 0.5f);

        DrawGridLayer(canvas, ox, oy, cw, ch, scale,
            spacing: 80f, color: NeonPalette.TextMuted.WithAlpha(0.04f), strokeWidth: 0.5f);
    }

    private static void DrawGridLayer(ICanvas canvas, float ox, float oy,
        float cw, float ch, float scale,
        float spacing, Color color, float strokeWidth)
    {
        canvas.StrokeSize = strokeWidth;
        canvas.StrokeColor = color;

        float spacingScaled = spacing * scale;
        if (spacingScaled < 2f) return;

        // Vertical lines
        for (float x = ox; x <= ox + cw; x += spacingScaled)
            canvas.DrawLine(x, oy, x, oy + ch);

        // Horizontal lines
        for (float y = oy; y <= oy + ch; y += spacingScaled)
            canvas.DrawLine(ox, y, ox + cw, y);
    }

    // ══════════════════════════════════════════════════════
    //  Cursor trail
    // ══════════════════════════════════════════════════════

    private void DrawCursorTrail(ICanvas canvas, float ox, float oy, float scale)
    {
        var trail = _s.Trail;
        if (trail == null || trail.Count < 2) return;

        float currentTime = _s.SimTime;
        const float maxAge = 0.3f;

        for (int i = 1; i < trail.Count; i++)
        {
            var prev = trail.GetByAge(i - 1);
            var curr = trail.GetByAge(i);

            float age = currentTime - curr.Time;
            if (age > maxAge || age < 0f) continue;

            float ageFactor = 1f - (age / maxAge);

            float thickness = 0.5f + ageFactor * 2.5f;

            float dx = curr.X - prev.X;
            float dy = curr.Y - prev.Y;
            float speed = MathF.Sqrt(dx * dx + dy * dy);
            float speedBoost = MathF.Min(speed * 0.002f, 0.3f);

            float alpha = ageFactor * 0.6f + speedBoost;

            canvas.StrokeSize = thickness;
            canvas.StrokeColor = NeonPalette.Cyan.WithAlpha(alpha);
            canvas.DrawLine(
                ox + prev.X * scale, oy + prev.Y * scale,
                ox + curr.X * scale, oy + curr.Y * scale);
        }
    }

    // ══════════════════════════════════════════════════════
    //  Cursor + click indicator
    // ══════════════════════════════════════════════════════

    private void DrawCursor(ICanvas canvas, float ox, float oy, float scale)
    {
        float cx = ox + _s.CursorX * scale;
        float cy = oy + _s.CursorY * scale;

        // Outer glow
        canvas.FillColor = NeonPalette.CyanGlow;
        canvas.FillCircle(cx, cy, 14);

        // Mid glow
        canvas.FillColor = NeonPalette.Cyan.WithAlpha(0.3f);
        canvas.FillCircle(cx, cy, 8);

        // Core dot — green when clicking
        canvas.FillColor = _s.PrimaryDown ? NeonPalette.Lime : NeonPalette.Cyan;
        canvas.FillCircle(cx, cy, 4);
    }

    // ══════════════════════════════════════════════════════
    //  HUD (minimal sandbox info)
    // ══════════════════════════════════════════════════════

    private void DrawHud(ICanvas canvas, float ox, float oy, float cw, float ch)
    {
        float hudY = oy + ch - 28;

        // Position readout (left)
        canvas.FontSize = 12;
        canvas.FontColor = NeonPalette.TextDim;
        canvas.DrawString($"({_s.CursorX:0}, {_s.CursorY:0})",
            ox + 16, hudY, HorizontalAlignment.Left);

        // Tick counter (right)
        canvas.FontSize = 12;
        canvas.FontColor = NeonPalette.TextDim;
        canvas.DrawString($"tick {_s.Tick}",
            ox + cw - 16, hudY, HorizontalAlignment.Right);
    }
}
