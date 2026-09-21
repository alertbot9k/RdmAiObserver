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
    public void Facts_do_not_count_the_local_player_as_an_ally()
    {
        var state = new GameState
        {
            TerritoryId = 1293,
            Player = new PlayerSnapshot { ObjectId = 42, Name = "Self", Hp = 50000, MaxHp = 58500 }
        };
        state.Party.Add(new PartyMemberSnapshot { ObjectId = 42, Name = "Self", Hp = 50000, MaxHp = 58500 });
        state.Party.Add(new PartyMemberSnapshot { ObjectId = 43, Name = "Ally", Hp = 50000, MaxHp = 58500, Distance = 5f });

        Assert.Equal(1, ObservationFacts.From(state, DateTime.UnixEpoch).AlliesWithin15Yalms);
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

    [Theory]
    [InlineData(0u, ObservedMatchPhase.Unknown)]
    [InlineData(1u, ObservedMatchPhase.Unknown)]
    public void Facts_do_not_call_unconfirmed_mode_active(uint territory, ObservedMatchPhase expected)
    {
        var state = new GameState { TerritoryId = territory, Player = new PlayerSnapshot { Hp = 1, MaxHp = 1 } };

        var facts = ObservationFacts.From(state, DateTime.UnixEpoch);

        Assert.Equal(expected, facts.MatchPhase);
    }

    [Fact]
    public void Facts_distinguish_respawn_and_spawn_protection()
    {
        var respawning = new GameState { TerritoryId = 1293, Player = new PlayerSnapshot { Hp = 0, MaxHp = 58500 } };
        var protectedState = new GameState { TerritoryId = 1293, Player = new PlayerSnapshot { Hp = 1, MaxHp = 58500 } };
        protectedState.Player.Statuses.Add(new StatusSnapshot { Name = "Invincibility" });

        Assert.Equal(ObservedMatchPhase.Respawning, ObservationFacts.From(respawning, DateTime.UnixEpoch).MatchPhase);
        Assert.Equal(ObservedMatchPhase.Protected, ObservationFacts.From(protectedState, DateTime.UnixEpoch).MatchPhase);
    }
}
