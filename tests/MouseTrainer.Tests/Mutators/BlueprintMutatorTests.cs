using MouseTrainer.Domain.Runs;
using MouseTrainer.Simulation.Levels;
using MouseTrainer.Simulation.Modes.ReflexGates;
using MouseTrainer.Simulation.Mutators;
using Xunit;

namespace MouseTrainer.Tests.Mutators;

/// <summary>
/// Tests for blueprint transform mutators: correctness, composition, immutability, registry.
/// All tests use a standard blueprint generated from seed 0xC0FFEE.
/// </summary>
public class BlueprintMutatorTests
{
    // ─────────────────────────────────────────────────────
    //  1. NarrowMargin
    // ─────────────────────────────────────────────────────

    [Fact]
    public void NarrowMargin_ScalesApertureHeight()
    {
        var blueprint = CreateTestBlueprint();
        var mutator = new NarrowMarginMutator(0.75f);
        var result = mutator.Apply(blueprint);

        Assert.Equal(blueprint.Gates.Count, result.Gates.Count);

        for (int i = 0; i < result.Gates.Count; i++)
        {
            float expected = blueprint.Gates[i].ApertureHeight * 0.75f;
            Assert.Equal(expected, result.Gates[i].ApertureHeight);
        }
    }

    [Fact]
    public void NarrowMargin_PreservesOtherFields()
    {
        var blueprint = CreateTestBlueprint();
        var mutator = new NarrowMarginMutator(0.75f);
        var result = mutator.Apply(blueprint);

        for (int i = 0; i < result.Gates.Count; i++)
        {
            Assert.Equal(blueprint.Gates[i].WallX, result.Gates[i].WallX);
            Assert.Equal(blueprint.Gates[i].RestCenterY, result.Gates[i].RestCenterY);
            Assert.Equal(blueprint.Gates[i].Amplitude, result.Gates[i].Amplitude);
            Assert.Equal(blueprint.Gates[i].Phase, result.Gates[i].Phase);
            Assert.Equal(blueprint.Gates[i].FreqHz, result.Gates[i].FreqHz);
        }

        Assert.Equal(blueprint.PlayfieldWidth, result.PlayfieldWidth);
        Assert.Equal(blueprint.PlayfieldHeight, result.PlayfieldHeight);
        Assert.Equal(blueprint.ScrollSpeed, result.ScrollSpeed);
    }

    [Fact]
    public void NarrowMargin_InvalidFactor_Low_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NarrowMarginMutator(0.05f));
    }

    [Fact]
    public void NarrowMargin_InvalidFactor_High_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NarrowMarginMutator(1.5f));
    }

    [Fact]
    public void NarrowMargin_FromSpec_UsesFactorParam()
    {
        var spec = MutatorSpec.Create(MutatorId.NarrowMargin,
            parameters: new[] { new MutatorParam("factor", 0.5f) });
        var mutator = new NarrowMarginMutator(spec);
        var blueprint = CreateTestBlueprint();
        var result = mutator.Apply(blueprint);

        Assert.Equal(blueprint.Gates[0].ApertureHeight * 0.5f, result.Gates[0].ApertureHeight);
    }

    [Fact]
    public void NarrowMargin_FromSpec_DefaultsWhenNoParam()
    {
        var spec = MutatorSpec.Create(MutatorId.NarrowMargin);
        var mutator = new NarrowMarginMutator(spec);
        var blueprint = CreateTestBlueprint();
        var result = mutator.Apply(blueprint);

        Assert.Equal(blueprint.Gates[0].ApertureHeight * 0.75f, result.Gates[0].ApertureHeight);
    }

    // ─────────────────────────────────────────────────────
    //  2. WideMargin
    // ─────────────────────────────────────────────────────

    [Fact]
    public void WideMargin_ScalesApertureHeight()
    {
        var blueprint = CreateTestBlueprint();
        var mutator = new WideMarginMutator(1.4f);
        var result = mutator.Apply(blueprint);

        for (int i = 0; i < result.Gates.Count; i++)
        {
            float expected = blueprint.Gates[i].ApertureHeight * 1.4f;
            Assert.Equal(expected, result.Gates[i].ApertureHeight);
        }
    }

    [Fact]
    public void WideMargin_InvalidFactor_Low_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WideMarginMutator(0.5f));
    }

    [Fact]
    public void WideMargin_InvalidFactor_High_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WideMarginMutator(4.0f));
    }

    // ─────────────────────────────────────────────────────
    //  3. DifficultyCurve
    // ─────────────────────────────────────────────────────

    [Fact]
    public void DifficultyCurve_ZeroCurve_PreservesEndpoints()
    {
        var blueprint = CreateTestBlueprint();
        var mutator = new DifficultyCurveMutator(0f);
        var result = mutator.Apply(blueprint);

        // First and last gate are the min/max anchors — they should be preserved exactly
        Assert.Equal(blueprint.Gates[0].ApertureHeight, result.Gates[0].ApertureHeight);
        Assert.Equal(blueprint.Gates[^1].ApertureHeight, result.Gates[^1].ApertureHeight);
        Assert.Equal(blueprint.Gates[0].Amplitude, result.Gates[0].Amplitude);
        Assert.Equal(blueprint.Gates[^1].Amplitude, result.Gates[^1].Amplitude);
        Assert.Equal(blueprint.Gates[0].FreqHz, result.Gates[0].FreqHz);
        Assert.Equal(blueprint.Gates[^1].FreqHz, result.Gates[^1].FreqHz);
    }

    [Fact]
    public void DifficultyCurve_ZeroCurve_PreservesPosition()
    {
        var blueprint = CreateTestBlueprint();
        var mutator = new DifficultyCurveMutator(0f);
        var result = mutator.Apply(blueprint);

        for (int i = 0; i < result.Gates.Count; i++)
        {
            Assert.Equal(blueprint.Gates[i].WallX, result.Gates[i].WallX);
            Assert.Equal(blueprint.Gates[i].RestCenterY, result.Gates[i].RestCenterY);
            Assert.Equal(blueprint.Gates[i].Phase, result.Gates[i].Phase);
        }
    }

    [Fact]
    public void DifficultyCurve_PositiveCurve_BackLoaded()
    {
        var blueprint = CreateTestBlueprint();
        var mutator = new DifficultyCurveMutator(1.0f);
        var result = mutator.Apply(blueprint);

        // Back-loaded: middle gates should have larger apertures (easier)
        // than the original linear ramp
        int mid = blueprint.Gates.Count / 2;
        Assert.True(result.Gates[mid].ApertureHeight > blueprint.Gates[mid].ApertureHeight,
            "Positive curve should make middle gates easier (larger aperture).");
    }

    [Fact]
    public void DifficultyCurve_NegativeCurve_FrontLoaded()
    {
        var blueprint = CreateTestBlueprint();
        var mutator = new DifficultyCurveMutator(-1.0f);
        var result = mutator.Apply(blueprint);

        // Front-loaded: middle gates should have smaller apertures (harder)
        // than the original linear ramp
        int mid = blueprint.Gates.Count / 2;
        Assert.True(result.Gates[mid].ApertureHeight < blueprint.Gates[mid].ApertureHeight,
            "Negative curve should make middle gates harder (smaller aperture).");
    }

    [Fact]
    public void DifficultyCurve_EndpointsAlwaysPreserved()
    {
        // Regardless of curve value, first and last gate should match min/max
        var blueprint = CreateTestBlueprint();

        foreach (var curve in new[] { -2.0f, -1.0f, 0.5f, 1.0f, 2.0f })
        {
            var mutator = new DifficultyCurveMutator(curve);
            var result = mutator.Apply(blueprint);

            Assert.Equal(blueprint.Gates[0].ApertureHeight, result.Gates[0].ApertureHeight);
            Assert.Equal(blueprint.Gates[^1].ApertureHeight, result.Gates[^1].ApertureHeight);
        }
    }

    [Fact]
    public void DifficultyCurve_InvalidCurve_Low_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DifficultyCurveMutator(-3.0f));
    }

    [Fact]
    public void DifficultyCurve_InvalidCurve_High_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DifficultyCurveMutator(3.0f));
    }

    [Fact]
    public void DifficultyCurve_SingleGate_ReturnsClone()
    {
        var singleGateBlueprint = new LevelBlueprint
        {
            Gates = new[]
            {
                new Gate { WallX = 100f, RestCenterY = 540f, ApertureHeight = 200f,
                    Amplitude = 40f, Phase = 1f, FreqHz = 0.5f },
            },
            PlayfieldWidth = 1920f,
            PlayfieldHeight = 1080f,
            ScrollSpeed = 70f,
        };

        var mutator = new DifficultyCurveMutator(1.0f);
        var result = mutator.Apply(singleGateBlueprint);

        Assert.NotSame(singleGateBlueprint, result);
        Assert.Equal(singleGateBlueprint.Gates[0].ApertureHeight, result.Gates[0].ApertureHeight);
    }

    // ─────────────────────────────────────────────────────
    //  4. Blueprint immutability
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Apply_DoesNotModifyOriginalBlueprint()
    {
        var blueprint = CreateTestBlueprint();

        // Snapshot original values
        var originalApertures = new float[blueprint.Gates.Count];
        for (int i = 0; i < blueprint.Gates.Count; i++)
            originalApertures[i] = blueprint.Gates[i].ApertureHeight;

        var mutator = new NarrowMarginMutator(0.5f);
        _ = mutator.Apply(blueprint);

        // Original should be unchanged
        for (int i = 0; i < blueprint.Gates.Count; i++)
            Assert.Equal(originalApertures[i], blueprint.Gates[i].ApertureHeight);
    }

    [Fact]
    public void Apply_ReturnsDifferentBlueprintInstance()
    {
        var blueprint = CreateTestBlueprint();
        var mutator = new NarrowMarginMutator(0.75f);
        var result = mutator.Apply(blueprint);

        Assert.NotSame(blueprint, result);
    }

    // ─────────────────────────────────────────────────────
    //  5. Pipeline composition
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Pipeline_AppliesInOrder()
    {
        var blueprint = CreateTestBlueprint();
        var registry = CreateDefaultRegistry();
        var pipeline = new MutatorPipeline(registry);

        // 0.5 * 2.0 = 1.0 net effect on aperture
        var specs = new[]
        {
            MutatorSpec.Create(MutatorId.NarrowMargin,
                parameters: new[] { new MutatorParam("factor", 0.5f) }),
            MutatorSpec.Create(MutatorId.WideMargin,
                parameters: new[] { new MutatorParam("factor", 2.0f) }),
        };

        var result = pipeline.Apply(blueprint, specs);

        for (int i = 0; i < result.Gates.Count; i++)
        {
            Assert.Equal(blueprint.Gates[i].ApertureHeight, result.Gates[i].ApertureHeight, precision: 3);
        }
    }

    [Fact]
    public void Pipeline_OrderMatters()
    {
        var blueprint = CreateTestBlueprint();
        var registry = CreateDefaultRegistry();
        var pipeline = new MutatorPipeline(registry);

        // Two DifficultyCurve mutators with different values.
        // The second curve re-interpolates from the output of the first,
        // so the order genuinely matters for intermediate gate values.
        var specsAB = new[]
        {
            MutatorSpec.Create(MutatorId.DifficultyCurve,
                parameters: new[] { new MutatorParam("curve", 1.5f) }),
            MutatorSpec.Create(MutatorId.DifficultyCurve,
                parameters: new[] { new MutatorParam("curve", -1.0f) }),
        };

        var specsBA = new[]
        {
            MutatorSpec.Create(MutatorId.DifficultyCurve,
                parameters: new[] { new MutatorParam("curve", -1.0f) }),
            MutatorSpec.Create(MutatorId.DifficultyCurve,
                parameters: new[] { new MutatorParam("curve", 1.5f) }),
        };

        var resultAB = pipeline.Apply(blueprint, specsAB);
        var resultBA = pipeline.Apply(blueprint, specsBA);

        // Different ordering should produce different intermediate gate values.
        // Endpoints (gate 0 and N-1) are always preserved at min/max, but middle gates differ.
        bool anyDiffer = false;
        for (int i = 1; i < resultAB.Gates.Count - 1; i++)
        {
            if (MathF.Abs(resultAB.Gates[i].ApertureHeight - resultBA.Gates[i].ApertureHeight) > 0.001f)
            {
                anyDiffer = true;
                break;
            }
        }

        Assert.True(anyDiffer, "Different mutator orders should produce different results.");
    }

    [Fact]
    public void Pipeline_EmptySpecs_ReturnsInputUnchanged()
    {
        var blueprint = CreateTestBlueprint();
        var registry = CreateDefaultRegistry();
        var pipeline = new MutatorPipeline(registry);

        var result = pipeline.Apply(blueprint, Array.Empty<MutatorSpec>());

        Assert.Same(blueprint, result);
    }

    [Fact]
    public void Pipeline_NarrowAppliedTwice_IsNotIdempotent()
    {
        var blueprint = CreateTestBlueprint();
        var registry = CreateDefaultRegistry();
        var pipeline = new MutatorPipeline(registry);

        var specs = new[]
        {
            MutatorSpec.Create(MutatorId.NarrowMargin,
                parameters: new[] { new MutatorParam("factor", 0.75f) }),
            MutatorSpec.Create(MutatorId.NarrowMargin,
                parameters: new[] { new MutatorParam("factor", 0.75f) }),
        };

        var result = pipeline.Apply(blueprint, specs);

        for (int i = 0; i < result.Gates.Count; i++)
        {
            float expected = blueprint.Gates[i].ApertureHeight * 0.75f * 0.75f;
            Assert.Equal(expected, result.Gates[i].ApertureHeight, precision: 3);
        }
    }

    // ─────────────────────────────────────────────────────
    //  6. Registry
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Registry_UnknownMutator_Throws()
    {
        var registry = new MutatorRegistry();
        var spec = MutatorSpec.Create(new MutatorId("Unknown"));

        Assert.Throws<InvalidOperationException>(() => registry.Resolve(spec));
    }

    [Fact]
    public void Registry_WrongVersion_Throws()
    {
        var registry = CreateDefaultRegistry();
        var spec = MutatorSpec.Create(MutatorId.NarrowMargin, version: 99);

        Assert.Throws<InvalidOperationException>(() => registry.Resolve(spec));
    }

    [Fact]
    public void Registry_ResolvesCorrectMutator()
    {
        var registry = CreateDefaultRegistry();
        var spec = MutatorSpec.Create(MutatorId.NarrowMargin);

        var mutator = registry.Resolve(spec);

        Assert.IsType<NarrowMarginMutator>(mutator);
    }

    // ─────────────────────────────────────────────────────
    //  7. RhythmLock
    // ─────────────────────────────────────────────────────

    [Fact]
    public void RhythmLock_QuantizesToNearestDivision()
    {
        // div=4 → step = π/2, allowed phases: {0, π/2, π, 3π/2}
        var mutator = new RhythmLockMutator(4);
        var blueprint = CreateTestBlueprint();
        var result = mutator.Apply(blueprint);

        float step = 2f * MathF.PI / 4f;

        for (int i = 0; i < result.Gates.Count; i++)
        {
            float phase = result.Gates[i].Phase;

            // Phase must be one of the allowed values: k * step for k in {0, 1, 2, 3}
            float k = phase / step;
            float roundedK = MathF.Round(k);
            Assert.True(MathF.Abs(k - roundedK) < 0.001f,
                $"Gate {i}: phase {phase} is not a multiple of step {step}. k={k}");
        }
    }

    [Fact]
    public void RhythmLock_PreservesAllOtherFields()
    {
        var blueprint = CreateTestBlueprint();
        var mutator = new RhythmLockMutator(4);
        var result = mutator.Apply(blueprint);

        Assert.Equal(blueprint.Gates.Count, result.Gates.Count);
        Assert.Equal(blueprint.PlayfieldWidth, result.PlayfieldWidth);
        Assert.Equal(blueprint.PlayfieldHeight, result.PlayfieldHeight);
        Assert.Equal(blueprint.ScrollSpeed, result.ScrollSpeed);

        for (int i = 0; i < result.Gates.Count; i++)
        {
            Assert.Equal(blueprint.Gates[i].WallX, result.Gates[i].WallX);
            Assert.Equal(blueprint.Gates[i].RestCenterY, result.Gates[i].RestCenterY);
            Assert.Equal(blueprint.Gates[i].ApertureHeight, result.Gates[i].ApertureHeight);
            Assert.Equal(blueprint.Gates[i].Amplitude, result.Gates[i].Amplitude);
            Assert.Equal(blueprint.Gates[i].FreqHz, result.Gates[i].FreqHz);
        }
    }

    [Fact]
    public void RhythmLock_IsIdempotent()
    {
        var blueprint = CreateTestBlueprint();
        var mutator = new RhythmLockMutator(4);
        var once = mutator.Apply(blueprint);
        var twice = mutator.Apply(once);

        for (int i = 0; i < once.Gates.Count; i++)
        {
            Assert.Equal(once.Gates[i].Phase, twice.Gates[i].Phase);
        }
    }

    [Fact]
    public void RhythmLock_PhaseInRange()
    {
        var blueprint = CreateTestBlueprint();
        var mutator = new RhythmLockMutator(4);
        var result = mutator.Apply(blueprint);

        float twoPi = 2f * MathF.PI;
        for (int i = 0; i < result.Gates.Count; i++)
        {
            Assert.True(result.Gates[i].Phase >= 0f,
                $"Gate {i}: phase {result.Gates[i].Phase} < 0");
            Assert.True(result.Gates[i].Phase < twoPi + 0.001f,
                $"Gate {i}: phase {result.Gates[i].Phase} >= 2π");
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    public void RhythmLock_AllowedDivisions_Succeed(int divisions)
    {
        var mutator = new RhythmLockMutator(divisions);
        var blueprint = CreateTestBlueprint();
        var result = mutator.Apply(blueprint);

        float step = 2f * MathF.PI / divisions;

        for (int i = 0; i < result.Gates.Count; i++)
        {
            float k = result.Gates[i].Phase / step;
            float roundedK = MathF.Round(k);
            Assert.True(MathF.Abs(k - roundedK) < 0.001f,
                $"div={divisions}, Gate {i}: phase not quantized. k={k}");
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(10)]
    public void RhythmLock_DisallowedDivisions_Throws(int divisions)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RhythmLockMutator(divisions));
    }

    [Fact]
    public void RhythmLock_FromSpec_UsesDivParam()
    {
        var spec = MutatorSpec.Create(MutatorId.RhythmLock,
            parameters: new[] { new MutatorParam("div", 6f) });
        var mutator = new RhythmLockMutator(spec);
        var blueprint = CreateTestBlueprint();
        var result = mutator.Apply(blueprint);

        float step = 2f * MathF.PI / 6f;

        for (int i = 0; i < result.Gates.Count; i++)
        {
            float k = result.Gates[i].Phase / step;
            float roundedK = MathF.Round(k);
            Assert.True(MathF.Abs(k - roundedK) < 0.001f,
                $"Gate {i}: phase not quantized to div=6. k={k}");
        }
    }

    [Fact]
    public void RhythmLock_FromSpec_DefaultsWhenNoParam()
    {
        var spec = MutatorSpec.Create(MutatorId.RhythmLock);
        var mutator = new RhythmLockMutator(spec);
        var blueprint = CreateTestBlueprint();
        var result = mutator.Apply(blueprint);

        // Default is div=4 → step = π/2
        float step = 2f * MathF.PI / 4f;

        for (int i = 0; i < result.Gates.Count; i++)
        {
            float k = result.Gates[i].Phase / step;
            float roundedK = MathF.Round(k);
            Assert.True(MathF.Abs(k - roundedK) < 0.001f,
                $"Gate {i}: phase not quantized with default div=4. k={k}");
        }
    }

    // ─────────────────────────────────────────────────────
    //  8. GateJitter
    // ─────────────────────────────────────────────────────

    [Fact]
    public void GateJitter_ModifiesRestCenterY()
    {
        var blueprint = CreateTestBlueprint();
        var mutator = new GateJitterMutator(0.5f);
        var result = mutator.Apply(blueprint);

        // At least some gates should have different RestCenterY
        bool anyDifferent = false;
        for (int i = 0; i < result.Gates.Count; i++)
        {
            if (MathF.Abs(result.Gates[i].RestCenterY - blueprint.Gates[i].RestCenterY) > 0.001f)
            {
                anyDifferent = true;
                break;
            }
        }
        Assert.True(anyDifferent, "GateJitter should modify at least some RestCenterY values.");
    }

    [Fact]
    public void GateJitter_PreservesOtherFields()
    {
        var blueprint = CreateTestBlueprint();
        var mutator = new GateJitterMutator(0.5f);
        var result = mutator.Apply(blueprint);

        Assert.Equal(blueprint.Gates.Count, result.Gates.Count);
        Assert.Equal(blueprint.PlayfieldWidth, result.PlayfieldWidth);
        Assert.Equal(blueprint.PlayfieldHeight, result.PlayfieldHeight);
        Assert.Equal(blueprint.ScrollSpeed, result.ScrollSpeed);

        for (int i = 0; i < result.Gates.Count; i++)
        {
            Assert.Equal(blueprint.Gates[i].WallX, result.Gates[i].WallX);
            Assert.Equal(blueprint.Gates[i].ApertureHeight, result.Gates[i].ApertureHeight);
            Assert.Equal(blueprint.Gates[i].Amplitude, result.Gates[i].Amplitude);
            Assert.Equal(blueprint.Gates[i].Phase, result.Gates[i].Phase);
            Assert.Equal(blueprint.Gates[i].FreqHz, result.Gates[i].FreqHz);
        }
    }

    [Fact]
    public void GateJitter_OffsetBoundedByMaxJitter()
    {
        // At max strength, offset should not exceed MaxJitterPx (25f)
        var blueprint = CreateTestBlueprint();
        var mutator = new GateJitterMutator(1.0f);
        var result = mutator.Apply(blueprint);

        for (int i = 0; i < result.Gates.Count; i++)
        {
            float offset = MathF.Abs(result.Gates[i].RestCenterY - blueprint.Gates[i].RestCenterY);
            Assert.True(offset <= 25f + 0.01f,
                $"Gate {i}: offset {offset} exceeds max jitter of 25px.");
        }
    }

    [Fact]
    public void GateJitter_ZeroStrength_NoChange()
    {
        var blueprint = CreateTestBlueprint();
        var mutator = new GateJitterMutator(0f);
        var result = mutator.Apply(blueprint);

        for (int i = 0; i < result.Gates.Count; i++)
        {
            Assert.Equal(blueprint.Gates[i].RestCenterY, result.Gates[i].RestCenterY);
        }
    }

    [Fact]
    public void GateJitter_RespectsCorridorMargins()
    {
        var blueprint = CreateTestBlueprint();
        var mutator = new GateJitterMutator(1.0f);
        var result = mutator.Apply(blueprint);

        float corridorMargin = 10f;

        for (int i = 0; i < result.Gates.Count; i++)
        {
            float halfAperture = result.Gates[i].ApertureHeight * 0.5f;
            float minY = halfAperture + corridorMargin;
            float maxY = blueprint.PlayfieldHeight - halfAperture - corridorMargin;

            if (minY <= maxY)
            {
                Assert.True(result.Gates[i].RestCenterY >= minY - 0.01f,
                    $"Gate {i}: centerY {result.Gates[i].RestCenterY} below corridor min {minY}.");
                Assert.True(result.Gates[i].RestCenterY <= maxY + 0.01f,
                    $"Gate {i}: centerY {result.Gates[i].RestCenterY} above corridor max {maxY}.");
            }
        }
    }

    [Fact]
    public void GateJitter_Deterministic()
    {
        var blueprint = CreateTestBlueprint();
        var mutator = new GateJitterMutator(0.5f);
        var resultA = mutator.Apply(blueprint);
        var resultB = mutator.Apply(blueprint);

        for (int i = 0; i < resultA.Gates.Count; i++)
        {
            Assert.Equal(resultA.Gates[i].RestCenterY, resultB.Gates[i].RestCenterY);
        }
    }

    [Fact]
    public void GateJitter_InvalidStrength_Low_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GateJitterMutator(-0.1f));
    }

    [Fact]
    public void GateJitter_InvalidStrength_High_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GateJitterMutator(1.1f));
    }

    [Fact]
    public void GateJitter_FromSpec_UsesStrParam()
    {
        var spec = MutatorSpec.Create(MutatorId.GateJitter,
            parameters: new[] { new MutatorParam("str", 0.7f) });
        var mutator = new GateJitterMutator(spec);
        var blueprint = CreateTestBlueprint();
        var result = mutator.Apply(blueprint);

        // At str=0.7, some offsets should be nonzero
        bool anyDifferent = false;
        for (int i = 0; i < result.Gates.Count; i++)
        {
            if (MathF.Abs(result.Gates[i].RestCenterY - blueprint.Gates[i].RestCenterY) > 0.001f)
            {
                anyDifferent = true;
                break;
            }
        }
        Assert.True(anyDifferent, "GateJitter str=0.7 should modify at least some gates.");
    }

    [Fact]
    public void GateJitter_FromSpec_DefaultsWhenNoParam()
    {
        var spec = MutatorSpec.Create(MutatorId.GateJitter);
        var mutator = new GateJitterMutator(spec);
        var blueprint = CreateTestBlueprint();
        var result = mutator.Apply(blueprint);

        // Default str=0.35, should still produce offsets
        bool anyDifferent = false;
        for (int i = 0; i < result.Gates.Count; i++)
        {
            if (MathF.Abs(result.Gates[i].RestCenterY - blueprint.Gates[i].RestCenterY) > 0.001f)
            {
                anyDifferent = true;
                break;
            }
        }
        Assert.True(anyDifferent, "GateJitter default should modify at least some gates.");
    }

    // ─────────────────────────────────────────────────────
    //  9. Pipeline — RhythmLock + GateJitter composition
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Pipeline_RhythmLockThenJitter_OrderMatters()
    {
        var blueprint = CreateTestBlueprint();
        var registry = CreateFullRegistry();
        var pipeline = new MutatorPipeline(registry);

        // RhythmLock changes Phase, which feeds into GateJitter's sin() offset.
        // So order should matter.
        var specsAB = new[]
        {
            MutatorSpec.Create(MutatorId.RhythmLock),
            MutatorSpec.Create(MutatorId.GateJitter),
        };

        var specsBA = new[]
        {
            MutatorSpec.Create(MutatorId.GateJitter),
            MutatorSpec.Create(MutatorId.RhythmLock),
        };

        var resultAB = pipeline.Apply(blueprint, specsAB);
        var resultBA = pipeline.Apply(blueprint, specsBA);

        // RhythmLock modifies Phase; GateJitter reads Phase for offset calculation.
        // In AB: quantized phases feed into jitter → different offsets than BA.
        // In BA: jitter uses original phases, then RhythmLock quantizes unchanged phases.
        // Phase values should be the same (RhythmLock is last in BA, only in AB),
        // but RestCenterY should differ because jitter input phases differ.
        bool anyDiffer = false;
        for (int i = 0; i < resultAB.Gates.Count; i++)
        {
            if (MathF.Abs(resultAB.Gates[i].RestCenterY - resultBA.Gates[i].RestCenterY) > 0.001f)
            {
                anyDiffer = true;
                break;
            }
        }

        Assert.True(anyDiffer,
            "RhythmLock+GateJitter vs GateJitter+RhythmLock should produce different RestCenterY values.");
    }

    [Fact]
    public void Pipeline_JitterThenNarrow_PreservesBothEffects()
    {
        var blueprint = CreateTestBlueprint();
        var registry = CreateFullRegistry();
        var pipeline = new MutatorPipeline(registry);

        var specs = new[]
        {
            MutatorSpec.Create(MutatorId.GateJitter,
                parameters: new[] { new MutatorParam("str", 0.5f) }),
            MutatorSpec.Create(MutatorId.NarrowMargin,
                parameters: new[] { new MutatorParam("factor", 0.5f) }),
        };

        var result = pipeline.Apply(blueprint, specs);

        // NarrowMargin should halve aperture heights
        for (int i = 0; i < result.Gates.Count; i++)
        {
            // The aperture should be half of original (NarrowMargin 0.5)
            Assert.Equal(blueprint.Gates[i].ApertureHeight * 0.5f, result.Gates[i].ApertureHeight, precision: 3);
        }

        // GateJitter should have modified at least some RestCenterY values
        bool anyDifferent = false;
        for (int i = 0; i < result.Gates.Count; i++)
        {
            if (MathF.Abs(result.Gates[i].RestCenterY - blueprint.Gates[i].RestCenterY) > 0.001f)
            {
                anyDifferent = true;
                break;
            }
        }
        Assert.True(anyDifferent, "GateJitter should have modified RestCenterY before NarrowMargin.");
    }

    // ─────────────────────────────────────────────────────
    //  10. Registry — new mutators resolve correctly
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Registry_ResolvesRhythmLock()
    {
        var registry = CreateFullRegistry();
        var spec = MutatorSpec.Create(MutatorId.RhythmLock);
        var mutator = registry.Resolve(spec);
        Assert.IsType<RhythmLockMutator>(mutator);
    }

    [Fact]
    public void Registry_ResolvesGateJitter()
    {
        var registry = CreateFullRegistry();
        var spec = MutatorSpec.Create(MutatorId.GateJitter);
        var mutator = registry.Resolve(spec);
        Assert.IsType<GateJitterMutator>(mutator);
    }

    // ─────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────

    private static LevelBlueprint CreateTestBlueprint()
    {
        var generator = new ReflexGateGenerator();
        var run = RunDescriptor.Create(ModeId.ReflexGates, 0xC0FFEEu);
        return generator.Generate(run);
    }

    private static MutatorRegistry CreateDefaultRegistry()
    {
        var registry = new MutatorRegistry();
        registry.Register(MutatorId.NarrowMargin, 1, spec => new NarrowMarginMutator(spec));
        registry.Register(MutatorId.WideMargin, 1, spec => new WideMarginMutator(spec));
        registry.Register(MutatorId.DifficultyCurve, 1, spec => new DifficultyCurveMutator(spec));
        return registry;
    }

    private static MutatorRegistry CreateFullRegistry()
    {
        var registry = CreateDefaultRegistry();
        registry.Register(MutatorId.RhythmLock, 1, spec => new RhythmLockMutator(spec));
        registry.Register(MutatorId.GateJitter, 1, spec => new GateJitterMutator(spec));
        return registry;
    }
}
