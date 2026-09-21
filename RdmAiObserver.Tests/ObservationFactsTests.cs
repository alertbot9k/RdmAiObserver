using SamplePlugin;
using Xunit;

namespace RdmAiObserver.Tests;

public sealed class ObservationFactsTests
{
    [Fact]
    public void Facts_make_missing_readiness_explicit_and_preserve_threat_fallback()
    {
        var captured = DateTime.UnixEpoch;
        var state = new GameState
        {
            TerritoryId = 1293,
            Player = new PlayerSnapshot { Hp = 50000, MaxHp = 58500, Mp = 5000, MaxMp = 10000 },
            Target = new TargetSnapshot { Name = "Enemy", Distance = 20, Hp = 50000, MaxHp = 58500 }
        };

        var facts = ObservationFacts.From(state, captured, captured.AddSeconds(1));

        Assert.Equal(ObservedPvpMode.CrystallineConflict, facts.Mode);
        Assert.Equal(ObservationEvidence.Confirmed, facts.ModeEvidence);
        Assert.True(facts.PlayerAlive);
        Assert.True(facts.TargetAlive);
        Assert.Equal(1, facts.EnemiesWithin25Yalms);
        Assert.Equal(ActionReadiness.Unknown, facts.Readiness("Purify"));
    }

    [Fact]
    public void Facts_mark_old_captures_as_observed_but_not_fresh()
    {
        var captured = DateTime.UnixEpoch;
        var state = new GameState { Player = new PlayerSnapshot { Hp = 1, MaxHp = 1 } };

        var facts = ObservationFacts.From(state, captured, captured.AddSeconds(6));

        Assert.Equal(ObservationEvidence.Observed, facts.Freshness);
        Assert.False(facts.IsFresh);
    }
}
