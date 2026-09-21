using SamplePlugin;
using System.Text.Json;
using Xunit;

namespace RdmAiObserver.Tests;

public sealed class ElixirSafetyTests
{
    [Fact]
    public void Selected_enemy_missing_from_nearby_list_blocks_elixir()
    {
        var state = RecoveryState();
        state.Target = new TargetSnapshot
        {
            Name = "Enemy One", Kind = "Pc", Distance = 12f, Hp = 50000, MaxHp = 58500
        };

        Assert.Equal(1, CombatProximity.CountEnemies(state, 25f));
        Assert.NotEqual("Use Standard-issue Elixir", DecisionEngine.Evaluate(state).Recommendation);
        var analysis = ReplayAnalyzer.Analyze([new RecordedGameState { State = state }]);
        Assert.Equal(0, analysis.ElixirOpportunitySnapshotCount);
    }

    [Fact]
    public void Selected_enemy_is_not_counted_twice()
    {
        var state = RecoveryState();
        state.Target = new TargetSnapshot
        {
            Name = "Enemy One", Kind = "Pc", Distance = 12f, Hp = 50000, MaxHp = 58500
        };
        state.NearbyCharacters.Add(new NearbyCharacterSnapshot
        {
            Name = "Enemy One", Kind = "Pc", Distance = 12f, Hp = 50000, MaxHp = 58500
        });

        Assert.Equal(1, CombatProximity.CountEnemies(state, 25f));
    }

    [Fact]
    public void Separate_nearby_characters_with_the_same_name_still_count_separately()
    {
        var state = RecoveryState();
        for (var index = 0; index < 2; index++)
            state.NearbyCharacters.Add(new NearbyCharacterSnapshot
            {
                Name = "Same Name", Kind = "Pc", Distance = 12f + index,
                Hp = 50000, MaxHp = 58500
            });

        Assert.Equal(2, CombatProximity.CountEnemies(state, 25f));
    }

    [Fact]
    public void Selected_enemy_interrupts_existing_elixir_cast()
    {
        var state = RecoveryState();
        state.Target = new TargetSnapshot
        {
            Name = "Enemy One", Kind = "Pc", Distance = 12f, Hp = 50000, MaxHp = 58500
        };
        state.Player!.Cast = new CastSnapshot { ActionId = PvpActionIds.StandardIssueElixir };

        Assert.Equal("Cancel Elixir and move", DecisionEngine.Evaluate(state).Recommendation);
    }

    [Fact]
    public void Unknown_elixir_readiness_does_not_recommend_it()
    {
        var state = RecoveryState();
        state.Player!.Actions.Clear();

        Assert.NotEqual("Use Standard-issue Elixir", DecisionEngine.Evaluate(state).Recommendation);
    }

    [Fact]
    public void Replay_with_missing_timestamp_does_not_throw()
    {
        var state = RecoveryState();
        var report = ReplayAnalyzer.CreateReport(
            [new RecordedGameState { State = state }], DateTime.UnixEpoch);

        Assert.Equal(1, report.Analysis.SnapshotCount);
    }

    [Fact]
    public void Recorded_state_round_trip_preserves_selected_target_threat()
    {
        var state = RecoveryState();
        state.Target = new TargetSnapshot
        {
            Name = "Enemy One", Kind = "Pc", Distance = 12f, Hp = 50000, MaxHp = 58500
        };
        var original = new RecordedGameState
        {
            CapturedAtUtc = DateTime.UnixEpoch,
            State = state
        };

        var restored = JsonSerializer.Deserialize<RecordedGameState>(JsonSerializer.Serialize(original));

        Assert.NotNull(restored);
        Assert.Equal(1, CombatProximity.CountEnemies(restored.State, 25f));
        Assert.Equal(0, ReplayAnalyzer.Analyze([restored]).ElixirOpportunitySnapshotCount);
    }

    private static GameState RecoveryState()
    {
        var state = new GameState
        {
            LoggedIn = true,
            Player = new PlayerSnapshot
            {
                Name = "Player", Hp = 35000, MaxHp = 58500, Mp = 3000, MaxMp = 10000
            }
        };
        state.Party.Add(new PartyMemberSnapshot { Name = "Player", Hp = 35000, MaxHp = 58500 });
        state.Player.Actions.Add(new ActionCooldownSnapshot
        {
            Name = "Standard-issue Elixir", IsAvailable = true, CurrentCharges = 1
        });
        return state;
    }
}
