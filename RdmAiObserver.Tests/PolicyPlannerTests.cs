using SamplePlugin;
using Xunit;

namespace RdmAiObserver.Tests;

public sealed class PolicyPlannerTests
{
    [Fact]
    public void Planner_refuses_to_plan_from_stale_facts()
    {
        var state = new GameState { TerritoryId = 1293, Player = new PlayerSnapshot { Hp = 50000, MaxHp = 58500 }, NearbyScanRadius = 60f };
        var facts = ObservationFacts.From(state, DateTime.UnixEpoch, DateTime.UnixEpoch.AddSeconds(10));

        var plan = PolicyPlanner.Plan(facts);

        Assert.Equal(PolicyPlanStatus.ObserveOnly, plan.Status);
        Assert.False(plan.IsActionable);
    }

    [Fact]
    public void Planner_adds_abort_conditions_to_a_confirmed_combo_sequence()
    {
        var state = new GameState
        {
            TerritoryId = 1293,
            NearbyScanRadius = 60f,
            NearbyScanComplete = true,
            Player = new PlayerSnapshot { Hp = 50000, MaxHp = 58500 },
            Target = new TargetSnapshot { Name = "Enemy", Hp = 50000, MaxHp = 58500, Distance = 5f }
        };
        state.Player.Statuses.Add(new StatusSnapshot { Name = "Enchanted Zwerchhau" });

        var plan = PolicyPlanner.Plan(ObservationFacts.From(state, DateTime.UnixEpoch));

        Assert.Equal(PolicyPlanStatus.Planned, plan.Status);
        Assert.Single(plan.Steps);
        Assert.Equal(ControlCommandKind.Action, plan.Steps[0].Kind);
        Assert.Equal("Enchanted Redoublement", plan.Steps[0].ActionName);
        Assert.All(plan.Steps, step => Assert.NotEmpty(step.AbortIf));
    }

    [Fact]
    public void Planner_types_target_and_movement_recommendations_without_fake_actions()
    {
        var targetState = new GameState
        {
            TerritoryId = 1293, NearbyScanRadius = 60f, NearbyScanComplete = true,
            Player = new PlayerSnapshot { Hp = 50000, MaxHp = 58500 }
        };
        targetState.NearbyCharacters.Add(new NearbyCharacterSnapshot
            { ObjectId = 42, Name = "Enemy", Hp = 30000, MaxHp = 58500, Distance = 10f });

        var targetPlan = PolicyPlanner.Plan(ObservationFacts.From(targetState, DateTime.UnixEpoch));
        Assert.Single(targetPlan.Steps);
        Assert.Equal(ControlCommandKind.SelectTarget, targetPlan.Steps[0].Kind);
        Assert.Equal(42UL, targetPlan.Steps[0].TargetObjectId);
        Assert.Null(targetPlan.Steps[0].ActionName);

        targetState.Target = new TargetSnapshot
            { ObjectId = 42, Name = "Enemy", Hp = 30000, MaxHp = 58500, Distance = 30f };
        var movementPlan = PolicyPlanner.Plan(ObservationFacts.From(targetState, DateTime.UnixEpoch));
        Assert.Single(movementPlan.Steps);
        Assert.Equal(ControlCommandKind.Move, movementPlan.Steps[0].Kind);
        Assert.Null(movementPlan.Steps[0].ActionName);
    }

    [Fact]
    public void Planner_models_unsafe_elixir_interruption_as_cancel_then_move()
    {
        var state = new GameState
        {
            TerritoryId = 1293, NearbyScanRadius = 60f, NearbyScanComplete = true,
            Player = new PlayerSnapshot
            {
                Hp = 40000, MaxHp = 58500,
                Cast = new CastSnapshot { ActionId = PvpActionIds.StandardIssueElixir }
            }
        };
        state.NearbyCharacters.Add(new NearbyCharacterSnapshot
            { ObjectId = 5, Name = "Enemy", Hp = 50000, MaxHp = 58500, Distance = 10f });

        var plan = PolicyPlanner.Plan(ObservationFacts.From(state, DateTime.UnixEpoch));

        Assert.Collection(plan.Steps,
            step => Assert.Equal(ControlCommandKind.CancelCast, step.Kind),
            step => Assert.Equal(ControlCommandKind.Move, step.Kind));
    }
}
