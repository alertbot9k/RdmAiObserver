using SamplePlugin;
using Xunit;

namespace RdmAiObserver.Tests;

public sealed class StrategicAwarenessTests
{
    public static IEnumerable<object[]> Scenarios => StrategicScenarioLibrary.All.Select(scenario => new object[] { scenario });

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Synthetic_crystal_strategy_matches_expected(StrategicScenario scenario) =>
        Assert.Equal(scenario.Expected, CrystalStrategyEvaluator.Evaluate(scenario.State, scenario.Movement).Strategy);

    [Fact]
    public void Team_awareness_reports_advantage_cluster_focus_and_retreat_direction()
    {
        var state = new GameState { TerritoryId = 1116, NearbyScanComplete = true,
            Player = new PlayerSnapshot { ObjectId = 1, Hp = 50000, MaxHp = 58500 } };
        state.Party.Add(new PartyMemberSnapshot { ObjectId = 2, Hp = 1, MaxHp = 1, Distance = 5, X = 10 });
        state.Party.Add(new PartyMemberSnapshot { ObjectId = 3, Hp = 1, MaxHp = 1, Distance = 7, X = 12 });
        state.NearbyCharacters.Add(new NearbyCharacterSnapshot { ObjectId = 4, Name = "Focus", Job = "black mage",
            Relation = CombatRelation.Hostile, Hp = 10000, MaxHp = 50000, Distance = 10, X = -10 });

        var result = TeamAwarenessEvaluator.Evaluate(state);

        Assert.Equal(2, result.NumericalAdvantage);
        Assert.True(result.AlliesClustered);
        Assert.Equal("Focus", result.FocusTarget?.Name);
        Assert.Contains("east", result.RetreatDirection);
        Assert.Equal(EvidenceConfidence.High, result.Confidence);
    }

    [Fact]
    public void Movement_tracker_uses_successive_coordinates()
    {
        var tracker = new MovementTrendTracker();
        var first = MovingState(0, 30, 30);
        var second = MovingState(4, 28, 24);
        Assert.Null(tracker.Update(first, DateTime.UnixEpoch));

        var trend = tracker.Update(second, DateTime.UnixEpoch.AddSeconds(2));

        Assert.NotNull(trend);
        Assert.Equal(2f, trend.PlayerSpeed);
        Assert.Equal(1f, trend.CrystalSpeed);
        Assert.True(trend.PlayerMovingTowardCrystal);
        Assert.True(trend.CrystalMoving);
    }

    [Fact]
    public void Unknown_mode_produces_low_confidence_observe_advice()
    {
        var state = new GameState { Player = new PlayerSnapshot { Hp = 50000, MaxHp = 58500 } };
        var result = DecisionEngine.Evaluate(state);
        Assert.Equal(DecisionPriority.Observe, result.Priority);
        Assert.Equal(RecommendationConfidence.Low, result.Confidence);
    }

    private static GameState MovingState(float playerX, float crystalX, float distance) => new()
    {
        TerritoryId = 1116,
        Player = new PlayerSnapshot { Hp = 1, MaxHp = 1, X = playerX },
        Objective = new ObjectiveSnapshot { BaseId = ObjectiveEvaluator.TacticalCrystalBaseId, X = crystalX, DistanceToPlayer = distance }
    };
}
