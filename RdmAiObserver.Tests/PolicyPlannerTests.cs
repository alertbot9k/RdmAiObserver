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
        Assert.True(plan.Steps.Count >= 2);
        Assert.All(plan.Steps, step => Assert.NotEmpty(step.AbortIf));
    }
}
