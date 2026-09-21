using SamplePlugin;
using System.Text.Json;
using Xunit;

namespace RdmAiObserver.Tests;

public sealed class ElixirSafetyTests
{
    [Fact]
    public void Confirmed_elixir_action_id_resolves_to_its_name()
    {
        Assert.True(PvpActionIds.TryGetKnownName(PvpActionIds.StandardIssueElixir, out var name));
        Assert.Equal("Standard-issue Elixir", name);
        Assert.False(PvpActionIds.TryGetKnownName(0, out _));
    }

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
    public void Object_identity_prevents_merging_distinct_same_named_targets()
    {
        var state = RecoveryState();
        state.Target = new TargetSnapshot
        {
            ObjectId = 100, Name = "Same Name", Kind = "Pc", Distance = 12f, Hp = 50000, MaxHp = 58500
        };
        state.NearbyCharacters.Add(new NearbyCharacterSnapshot
        {
            ObjectId = 200, Name = "Same Name", Kind = "Pc", Distance = 12f, Hp = 50000, MaxHp = 58500
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
    public void Purify_takes_priority_over_recovery_when_crowd_controlled()
    {
        var state = RecoveryState();
        state.Player!.Statuses.Add(new StatusSnapshot { Name = "Stun", RemainingSeconds = 2f });
        state.Player.Actions.Add(new ActionCooldownSnapshot
        {
            Name = "Purify", IsAvailable = true, CurrentCharges = 1
        });

        Assert.Equal("Purify", DecisionEngine.Evaluate(state).Recommendation);
    }

    [Fact]
    public void Active_guard_takes_priority_over_purify_and_recovery()
    {
        var state = RecoveryState();
        state.Player!.Statuses.Add(new StatusSnapshot { Name = "Guard", RemainingSeconds = 2f });
        state.Player.Actions.Add(new ActionCooldownSnapshot
        {
            Name = "Purify", IsAvailable = true, CurrentCharges = 1
        });

        Assert.Equal("Hold Guard", DecisionEngine.Evaluate(state).Recommendation);
    }

    [Fact]
    public void Critical_health_does_not_start_elixir_cast()
    {
        var state = RecoveryState();
        state.Player!.Hp = 12000;

        Assert.NotEqual("Use Standard-issue Elixir", DecisionEngine.Evaluate(state).Recommendation);
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
        Assert.Equal(ReplayReportSchema.CurrentVersion, report.FormatVersion);
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

    [Fact]
    public void Recorded_state_round_trip_preserves_objective_discovery_identity_and_position()
    {
        var state = RecoveryState();
        state.NearbyWorldObjects.Add(new WorldObjectSnapshot
        {
            GameObjectId = 100, EntityId = 200, BaseId = 300,
            Name = "Candidate", Kind = "EventObj", SubKind = 4,
            IsTargetable = true, Distance = 12.5f, X = 1f, Y = 2f, Z = 3f
        });

        var restored = JsonSerializer.Deserialize<RecordedGameState>(JsonSerializer.Serialize(
            new RecordedGameState { CapturedAtUtc = DateTime.UnixEpoch, State = state }));

        var candidate = Assert.Single(restored!.State.NearbyWorldObjects);
        Assert.Equal(300U, candidate.BaseId);
        Assert.Equal(1f, candidate.X);
        Assert.Equal(3f, candidate.Z);
    }

    [Fact]
    public void Replay_orders_out_of_order_snapshots_before_analysis()
    {
        var early = RecoveryState();
        early.Player!.Hp = 58500;
        var late = RecoveryState();
        late.Player!.Hp = 12000;
        var report = ReplayAnalyzer.CreateReport(
            [
                new RecordedGameState { CapturedAtUtc = DateTime.UnixEpoch.AddSeconds(2), State = late },
                new RecordedGameState { CapturedAtUtc = DateTime.UnixEpoch.AddSeconds(1), State = early }
            ], DateTime.UnixEpoch);

        Assert.Equal(DateTime.UnixEpoch.AddSeconds(1), report.Timeline[0].CapturedAtUtc);
        Assert.Equal(DateTime.UnixEpoch.AddSeconds(2), report.Timeline[^1].CapturedAtUtc);
    }

    private static GameState RecoveryState()
    {
        var state = new GameState
        {
            LoggedIn = true,
            TerritoryId = 1293,
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
