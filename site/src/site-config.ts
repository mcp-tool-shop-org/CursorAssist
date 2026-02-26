import type { SiteConfig } from '@mcptoolshop/site-theme';

export const config: SiteConfig = {
  title: 'CursorAssist',
  description: 'A deterministic engine for assistive cursor control, UI accessibility benchmarking, and adaptive motor-skill training.',
  logoBadge: 'CA',
  brandName: 'CursorAssist',
  repoUrl: 'https://github.com/mcp-tool-shop-org/CursorAssist',
  footerText: 'MIT Licensed — built by <a href="https://github.com/mcp-tool-shop-org" style="color:var(--color-muted);text-decoration:underline">mcp-tool-shop-org</a>',

  hero: {
    badge: 'Accessibility',
    headline: 'Assistive cursor control,',
    headlineAccent: 'deterministic by design.',
    description: 'DSP-grounded tremor compensation, adaptive deadzones, and target magnetism — every frame is hash-verified and reproducible. Plus MouseTrainer for building dexterity over time.',
    primaryCta: { href: '#usage', label: 'Get started' },
    secondaryCta: { href: '#packages', label: 'See packages' },
    previews: [
      { label: 'Install', code: 'dotnet add package CursorAssist.Engine' },
      { label: 'Pipeline', code: 'var pipeline = new TransformPipeline()\n  .Add(new SoftDeadzoneTransform())\n  .Add(new SmoothingTransform())\n  .Add(new PhaseCompensationTransform());' },
      { label: 'Run', code: 'var result = engine.FixedStep(in raw, ctx);\n// result.DeterminismHash → FNV-1a' },
    ],
  },

  sections: [
    {
      kind: 'features',
      id: 'features',
      title: 'Features',
      subtitle: 'DSP-grounded accessibility, not toy filters.',
      features: [
        { title: 'Tremor compensation', desc: 'Velocity-adaptive EMA smoothing with closed-form cutoff from tremor frequency, optional dual-pole, and phase compensation.' },
        { title: 'Fully deterministic', desc: 'Same input always produces the same output. No DateTime.Now, no Random, no platform-dependent floats. FNV-1a hash verification on every tick.' },
        { title: 'Modular NuGet packages', desc: 'Canon (schemas), Trace (recording), Policy (profile mapping), Engine (pipeline). Use only what you need — no forced coupling.' },
      ],
    },
    {
      kind: 'code-cards',
      id: 'usage',
      title: 'Usage',
      cards: [
        {
          title: 'Map a motor profile',
          code: 'using CursorAssist.Canon.Schemas;\nusing CursorAssist.Policy;\n\nvar profile = new MotorProfile {\n  TremorFrequencyHz = 6f,\n  TremorAmplitudeVpx = 4.5f,\n  PathEfficiency = 0.72f,\n};\n\nvar config = ProfileToConfigMapper.Map(profile);\n// SmoothingMinAlpha → ~0.31\n// DeadzoneRadiusVpx → ~2.7',
        },
        {
          title: 'Build the pipeline',
          code: 'using CursorAssist.Engine.Core;\nusing CursorAssist.Engine.Transforms;\n\nvar pipeline = new TransformPipeline()\n  .Add(new SoftDeadzoneTransform())\n  .Add(new SmoothingTransform())\n  .Add(new PhaseCompensationTransform())\n  .Add(new TargetMagnetismTransform());\n\nvar engine = new DeterministicPipeline(\n  pipeline, fixedHz: 60);',
        },
      ],
    },
    {
      kind: 'data-table',
      id: 'packages',
      title: 'NuGet Packages',
      subtitle: 'Four independent packages, one-way dependencies.',
      columns: ['Package', 'Depends on', 'Description'],
      rows: [
        ['CursorAssist.Canon', '(nothing)', 'Versioned immutable schemas and DTOs'],
        ['CursorAssist.Trace', '(nothing)', 'JSONL trace format for cursor recording'],
        ['CursorAssist.Policy', 'Canon', 'Deterministic profile-to-config mapper'],
        ['CursorAssist.Engine', 'Canon, Trace', 'Transform pipeline with 60 Hz accumulator'],
      ],
    },
    {
      kind: 'features',
      id: 'products',
      title: 'Two Products, One Engine',
      subtitle: 'Real-time assistance and dexterity training.',
      features: [
        { title: 'CursorAssist Pilot', desc: 'System tray app that intercepts and transforms raw pointer input in real time — tremor compensation, deadzones, edge resistance, target magnetism.' },
        { title: 'MouseTrainer', desc: '.NET MAUI desktop game with fixed-timestep simulation, composable blueprint mutators, and drag-and-drop gauntlet mode for building dexterity.' },
        { title: '214+ tests', desc: 'Every transform, every policy rule, every edge case. Platform-stable run identity via xorshift32 RNG and FNV-1a hashing.' },
      ],
    },
  ],
};
