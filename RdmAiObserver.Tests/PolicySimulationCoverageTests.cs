using SamplePlugin;
using Xunit;

namespace RdmAiObserver.Tests;

public sealed class PolicySimulationCoverageTests
{
    private static GameState BaseState() => new()
    {
        TerritoryId = 1293, NearbyScanRadius = 60f, NearbyScanComplete = true,
        Player = new PlayerSnapshot { Hp = 50000, MaxHp = 58500 },
        Target = new TargetSnapshot { ObjectId = 10, Name = "Enemy", Hp = 50000, MaxHp = 58500, Distance = 5f }
    };

    [Fact]
    public void Cast_interrupt_window_without_a_confirmed_action_abstains()
    {
        var state = BaseState();
        state.Target!.Cast = new CastSnapshot { IsInterruptible = true };
        var facts = ObservationFacts.From(state, DateTime.UnixEpoch);
        var plan = PolicyPlanner.Plan(facts);
        Assert.Equal(PolicyPlanStatus.ObserveOnly, plan.Status);
        Assert.Empty(plan.Steps);
    }

    [Fact]
    public void Target_swap_is_reflected_between_recorded_states()
    {
        var first = BaseState();
        var second = BaseState();
        second.Target = new TargetSnapshot { ObjectId = 20, Name = "Other", Hp = 40000, MaxHp = 58500, Distance = 5f };
        var report = ReplayAnalyzer.CreateReport([
            new RecordedGameState { CapturedAtUtc = DateTime.UnixEpoch, State = first },
            new RecordedGameState { CapturedAtUtc = DateTime.UnixEpoch.AddSeconds(1), State = second }], DateTime.UnixEpoch);
        Assert.Contains(report.Timeline, item => item.TargetName == "Other");
    }

    [Fact]
    public void Guard_and_purify_states_produce_defensive_facts()
    {
        var state = BaseState();
        state.Player!.Statuses.Add(new StatusSnapshot { Name = "Guard" });
        state.Player.Statuses.Add(new StatusSnapshot { Name = "Stun" });
        var facts = ObservationFacts.From(state, DateTime.UnixEpoch);
        Assert.Equal(MitigationState.Guarding, facts.PlayerMitigation);
        Assert.True(facts.PlayerCrowdControlled);
    }

    [Fact]
    public void Respawn_transition_enters_recovery_mode()
    {
        var state = BaseState();
        state.Player!.Hp = 0;
        var plan = PolicyPlanner.Plan(ObservationFacts.From(state, DateTime.UnixEpoch));
        Assert.Equal(PolicyPlanStatus.Recover, plan.Status);
    }

    [Fact]
    public void Missing_and_stale_scans_are_observe_only()
    {
        var missing = BaseState();
        missing.NearbyScanRadius = 0;
        missing.NearbyScanComplete = false;
        Assert.Equal(PolicyPlanStatus.ObserveOnly, PolicyPlanner.Plan(ObservationFacts.From(missing, DateTime.UnixEpoch)).Status);

        var stale = ObservationFacts.From(BaseState(), DateTime.UnixEpoch, DateTime.UnixEpoch.AddSeconds(10));
        Assert.Equal(PolicyPlanStatus.ObserveOnly, PolicyPlanner.Plan(stale).Status);
    }

    [Fact]
    public void Rate_limit_exhaustion_rejects_excess_commands()
    {
        var state = BaseState();
        state.Player!.Statuses.Add(new StatusSnapshot { Name = "Enchanted Redoublement" });
        state.Player.Actions.Add(new ActionCooldownSnapshot
            { Name = "Displacement", IsAvailable = true, CurrentCharges = 1 });
        var result = PolicySimulator.Run([new RecordedGameState { CapturedAtUtc = DateTime.UnixEpoch, State = state }],
            new SafetyPolicy(new HashSet<string> { "Displacement", "Scorch" }, MaxCommandsPerWindow: 1), DateTime.UnixEpoch);
        Assert.True(result.RejectedCommands >= 1);
    }
}
