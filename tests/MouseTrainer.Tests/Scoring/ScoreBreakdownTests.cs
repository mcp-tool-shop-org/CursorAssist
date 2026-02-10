using MouseTrainer.Domain.Events;
using MouseTrainer.Domain.Scoring;
using MouseTrainer.Simulation.Session;
using Xunit;

namespace MouseTrainer.Tests.Scoring;

public class ScoreBreakdownTests
{
    // ── 1. Golden breakdown for known events ──

    [Fact]
    public void GoldenBreakdown_ThreeGatesOneMiss()
    {
        var sc = CreatePlayingSession();

        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.EnteredGate, Intensity: 1f, Arg0: 0, Arg1: 100)
        });
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.EnteredGate, Intensity: 0.9f, Arg0: 1, Arg1: 90)
        });
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.HitWall, Intensity: 0.5f, Arg0: 2, Arg1: 500)
        });
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.LevelComplete, Intensity: 1f, Arg0: 190, Arg1: 0)
        });

        var result = sc.GetResult();
        Assert.NotNull(result);
        Assert.NotNull(result.Breakdown);

        var bd = result.Breakdown;
        Assert.Equal(190, bd.Total);
        Assert.Equal(3, bd.DeltaCount);
        Assert.Single(bd.TotalsByComponent);
        Assert.Equal(190, bd.TotalsByComponent[ScoreComponentId.GateScore]);
    }

    // ── 2. Conservation invariant ──

    [Fact]
    public void Conservation_SumOfComponents_EqualsTotal()
    {
        var sc = CreatePlayingSession();

        for (int i = 0; i < 5; i++)
        {
            sc.ApplyEvents(new List<GameEvent>
            {
                new(GameEventType.EnteredGate, Intensity: 1f, Arg0: i, Arg1: 50 + i * 10)
            });
        }
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.LevelComplete, Intensity: 1f, Arg0: 0, Arg1: 0)
        });

        var bd = sc.GetResult()!.Breakdown!;
        int componentSum = bd.TotalsByComponent.Values.Sum();
        Assert.Equal(bd.Total, componentSum);
    }

    // ── 3. Determinism: same events, identical breakdown ──

    [Fact]
    public void Determinism_SameEvents_IdenticalBreakdown()
    {
        var events = new List<GameEvent>
        {
            new(GameEventType.EnteredGate, Intensity: 1f, Arg0: 0, Arg1: 100),
            new(GameEventType.HitWall, Intensity: 0.5f, Arg0: 1, Arg1: 500),
            new(GameEventType.EnteredGate, Intensity: 0.8f, Arg0: 2, Arg1: 80),
            new(GameEventType.LevelComplete, Intensity: 1f, Arg0: 0, Arg1: 0),
        };

        var bd1 = RunSession(events);
        var bd2 = RunSession(events);

        Assert.Equal(bd1.Total, bd2.Total);
        Assert.Equal(bd1.DeltaCount, bd2.DeltaCount);
        Assert.Equal(
            bd1.TotalsByComponent[ScoreComponentId.GateScore],
            bd2.TotalsByComponent[ScoreComponentId.GateScore]);
    }

    // ── 4. Combo tracking + delta count ──

    [Fact]
    public void ComboTracking_DeltaCountMatchesGateEvents()
    {
        var sc = CreatePlayingSession();

        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.EnteredGate, Intensity: 1f, Arg0: 0, Arg1: 100)
        });
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.EnteredGate, Intensity: 0.9f, Arg0: 1, Arg1: 90)
        });
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.HitWall, Intensity: 0.5f, Arg0: 2, Arg1: 500)
        });
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.EnteredGate, Intensity: 0.9f, Arg0: 3, Arg1: 90)
        });
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.LevelComplete, Intensity: 1f, Arg0: 0, Arg1: 0)
        });

        var bd = sc.GetResult()!.Breakdown!;
        Assert.Equal(4, bd.DeltaCount);
        Assert.Equal(280, bd.Total);
    }

    // ── 5. HitWall only ──

    [Fact]
    public void HitWallOnly_BreakdownTotalIsZero()
    {
        var sc = CreatePlayingSession();

        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.HitWall, Intensity: 0.5f, Arg0: 0, Arg1: 500)
        });
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.HitWall, Intensity: 0.5f, Arg0: 1, Arg1: 400)
        });
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.LevelComplete, Intensity: 1f, Arg0: 0, Arg1: 0)
        });

        var bd = sc.GetResult()!.Breakdown!;
        Assert.Equal(0, bd.Total);
        Assert.Equal(2, bd.DeltaCount);
        Assert.Equal(0, bd.TotalsByComponent[ScoreComponentId.GateScore]);
    }

    // ── 6. DeltaCount == gate result count ──

    [Fact]
    public void DeltaCount_EqualsGateResultCount()
    {
        var sc = CreatePlayingSession();

        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.EnteredGate, Intensity: 1f, Arg0: 0, Arg1: 100)
        });
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.HitWall, Intensity: 0.5f, Arg0: 1, Arg1: 500)
        });
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.EnteredGate, Intensity: 0.8f, Arg0: 2, Arg1: 80)
        });
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.LevelComplete, Intensity: 1f, Arg0: 0, Arg1: 0)
        });

        var result = sc.GetResult()!;
        Assert.Equal(result.Gates.Count, result.Breakdown!.DeltaCount);
    }

    // ── 7. Backward compat ──

    [Fact]
    public void BackwardCompat_SessionResult_WithoutBreakdown()
    {
        var result = new SessionResult(
            Seed: 0xC0FFEEu,
            Elapsed: TimeSpan.FromSeconds(30),
            TotalScore: 500,
            MaxCombo: 5,
            GatesPassed: 10,
            GatesTotal: 12,
            Gates: new List<GateResult>().AsReadOnly());

        Assert.Null(result.RunId);
        Assert.Null(result.Breakdown);
        Assert.Equal(500, result.TotalScore);
    }

    // ── 8. Breakdown total matches TotalScore ──

    [Fact]
    public void BreakdownTotal_MatchesTotalScore()
    {
        var sc = CreatePlayingSession();

        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.EnteredGate, Intensity: 1f, Arg0: 0, Arg1: 100)
        });
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.EnteredGate, Intensity: 0.9f, Arg0: 1, Arg1: 90)
        });
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.HitWall, Intensity: 0.5f, Arg0: 2, Arg1: 500)
        });
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.EnteredGate, Intensity: 0.8f, Arg0: 3, Arg1: 80)
        });
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.LevelComplete, Intensity: 1f, Arg0: 0, Arg1: 0)
        });

        var result = sc.GetResult()!;
        Assert.Equal(result.TotalScore, result.Breakdown!.Total);
    }

    // ── 9. Empty session ──

    [Fact]
    public void EmptySession_ZeroTotalZeroDeltas()
    {
        var sc = CreatePlayingSession();
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.LevelComplete, Intensity: 1f, Arg0: 0, Arg1: 0)
        });

        var bd = sc.GetResult()!.Breakdown!;
        Assert.Equal(0, bd.Total);
        Assert.Equal(0, bd.DeltaCount);
        Assert.Empty(bd.TotalsByComponent);
    }

    // ── 10. Reset clears deltas ──

    [Fact]
    public void ResetToReady_ClearsDeltas()
    {
        var sc = CreatePlayingSession();
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.EnteredGate, Intensity: 1f, Arg0: 0, Arg1: 100)
        });

        sc.ResetToReady(0xBEEFu, 8);
        sc.Start();
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.EnteredGate, Intensity: 0.9f, Arg0: 0, Arg1: 50)
        });
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.LevelComplete, Intensity: 1f, Arg0: 0, Arg1: 0)
        });

        var bd = sc.GetResult()!.Breakdown!;
        Assert.Equal(50, bd.Total);
        Assert.Equal(1, bd.DeltaCount);
    }

    // ── Helpers ──

    private static SessionController CreatePlayingSession()
    {
        var sc = new SessionController();
        sc.ResetToReady(0xC0FFEEu, 12);
        sc.Start();
        return sc;
    }

    private static ScoreBreakdown RunSession(IReadOnlyList<GameEvent> events)
    {
        var sc = new SessionController();
        sc.ResetToReady(0xC0FFEEu, 12);
        sc.Start();
        sc.ApplyEvents(events);
        return sc.GetResult()!.Breakdown!;
    }
}
