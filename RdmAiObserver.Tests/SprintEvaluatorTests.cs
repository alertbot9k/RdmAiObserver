using SamplePlugin;
using Xunit;

namespace RdmAiObserver.Tests;

public sealed class SprintEvaluatorTests
{
    [Fact]
    public void Does_not_recommend_sprint_outside_confirmed_cc() =>
        Assert.Equal("No Sprint advice", SprintEvaluator.Evaluate(ReadyState(0)).Recommendation);

    [Fact]
    public void Recommends_sprint_for_safe_long_crystal_travel()
    {
        var state = ReadyState();
        state.Objective = new ObjectiveSnapshot
            { BaseId = ObjectiveEvaluator.TacticalCrystalBaseId, DistanceToPlayer = 40f };
        Assert.Equal("Use Sprint toward the crystal", SprintEvaluator.Evaluate(state).Recommendation);
    }

    [Fact]
    public void Recommends_sprint_to_escape_isolation()
    {
        var state = ReadyState();
        state.NearbyCharacters.Add(new NearbyCharacterSnapshot
            { ObjectId = 2, Relation = CombatRelation.Hostile, Hp = 1, MaxHp = 1, Distance = 5f });
        state.NearbyCharacters.Add(new NearbyCharacterSnapshot
            { ObjectId = 3, Relation = CombatRelation.Hostile, Hp = 1, MaxHp = 1, Distance = 8f });
        Assert.Equal("Use Sprint to disengage", SprintEvaluator.Evaluate(state).Recommendation);
    }

    [Fact]
    public void Preserves_active_sprint()
    {
        var state = ReadyState();
        state.Player!.Statuses.Add(new StatusSnapshot { Name = "Sprint" });
        Assert.Equal("Keep Sprint active", SprintEvaluator.Evaluate(state).Recommendation);
    }

    private static GameState ReadyState(uint territoryId = 1116)
    {
        var state = new GameState
        {
            LoggedIn = true,
            TerritoryId = territoryId,
            NearbyScanComplete = true,
            Player = new PlayerSnapshot { ObjectId = 1, Hp = 50000, MaxHp = 58500 }
        };
        state.Player.Actions.Add(new ActionCooldownSnapshot { Name = "Sprint", IsAvailable = true });
        return state;
    }
}
