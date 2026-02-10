namespace MouseTrainer.MauiHost;

/// <summary>
/// Lightweight particle system for gate pass/miss visual feedback.
/// Pre-allocated pool of 32 particles. No heap allocation per frame.
/// </summary>
public sealed class ParticleSystem
{
    public struct Particle
    {
        public float X, Y;
        public float VX, VY;
        public float Life;
        public float MaxLife;
        public float Size;
        public Color Color;
        public bool Active;
    }

    private readonly Particle[] _pool;
    private int _activeCount;

    public int ActiveCount => _activeCount;

    public ParticleSystem(int capacity = 32)
    {
        _pool = new Particle[capacity];
    }

    /// <summary>
    /// Spawn a ring burst (gate pass): particles expand outward from center.
    /// </summary>
    public void SpawnPassBurst(float centerX, float centerY, Color color, int count = 8)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (i / (float)count) * MathF.Tau;
            float speed = 80f + (i % 3) * 40f;
            SpawnOne(centerX, centerY,
                MathF.Cos(angle) * speed,
                MathF.Sin(angle) * speed,
                life: 0.25f, size: 3f, color: color);
        }
    }

    /// <summary>
    /// Spawn a miss pulse: fewer, larger, red particles.
    /// </summary>
    public void SpawnMissBurst(float centerX, float centerY, int count = 4)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (i / (float)count) * MathF.Tau + 0.3f;
            SpawnOne(centerX, centerY,
                MathF.Cos(angle) * 50f,
                MathF.Sin(angle) * 50f,
                life: 0.3f, size: 5f, color: NeonPalette.RedMagenta);
        }
    }

    private void SpawnOne(float x, float y, float vx, float vy,
                          float life, float size, Color color)
    {
        for (int i = 0; i < _pool.Length; i++)
        {
            if (!_pool[i].Active)
            {
                _pool[i] = new Particle
                {
                    X = x, Y = y, VX = vx, VY = vy,
                    Life = life, MaxLife = life,
                    Size = size, Color = color, Active = true
                };
                _activeCount++;
                return;
            }
        }
        // Pool full — silently drop (cosmetic only)
    }

    /// <summary>
    /// Advance all particles by dt seconds.
    /// </summary>
    public void Update(float dt)
    {
        for (int i = 0; i < _pool.Length; i++)
        {
            if (!_pool[i].Active) continue;
            ref var p = ref _pool[i];
            p.X += p.VX * dt;
            p.Y += p.VY * dt;
            p.Life -= dt;
            if (p.Life <= 0f)
            {
                p.Active = false;
                _activeCount--;
            }
        }
    }

    /// <summary>
    /// Draw all active particles onto the canvas.
    /// </summary>
    public void Draw(ICanvas canvas, float ox, float oy, float scale)
    {
        for (int i = 0; i < _pool.Length; i++)
        {
            if (!_pool[i].Active) continue;
            ref readonly var p = ref _pool[i];

            float alpha = p.Life / p.MaxLife;
            float screenX = ox + p.X * scale;
            float screenY = oy + p.Y * scale;
            float screenSize = MathF.Max(p.Size * scale, 1f);

            canvas.FillColor = p.Color.WithAlpha(alpha * 0.8f);
            canvas.FillCircle(screenX, screenY, screenSize);
        }
    }

    public void Clear()
    {
        for (int i = 0; i < _pool.Length; i++)
            _pool[i].Active = false;
        _activeCount = 0;
    }
}
