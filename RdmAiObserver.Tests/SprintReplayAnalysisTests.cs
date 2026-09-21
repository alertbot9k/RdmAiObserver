using SamplePlugin;
using Xunit;

namespace RdmAiObserver.Tests;

public sealed class SprintReplayAnalysisTests
{
    [Fact]
    public void Counts_recommended_and_missed_sprint_opportunity()
    {
        var opportunity = State();
        opportunity.Objective = new ObjectiveSnapshot
            { BaseId = ObjectiveEvaluator.TacticalCrystalBaseId, DistanceToPlayer = 40f };
        var combat = State();
        combat.Target = new TargetSnapshot { Hp = 50000, MaxHp = 58500, Distance = 10f };
        combat.NearbyCharacters.Add(new NearbyCharacterSnapshot
            { Relation = CombatRelation.Hostile, Hp = 1, MaxHp = 1, Distance = 10f });

        var analysis = ReplayAnalyzer.Analyze(Frames(opportunity, combat));

        Assert.Equal(1, analysis.SprintRecommendedUseOpportunityCount);
        Assert.Equal(1, analysis.SprintMissedOpportunityCount);
    }

    [Fact]
    public void Estimates_active_sprint_duration_from_adjacent_samples()
    {
        var first = State();
        first.Player!.Statuses.Add(new StatusSnapshot { Name = "Sprint" });
        var second = State();
        second.Player!.Statuses.Add(new StatusSnapshot { Name = "Sprint" });

        var analysis = ReplayAnalyzer.Analyze(Frames(first, second));

        Assert.Equal(2f, analysis.SprintActiveDurationSeconds);
    }

    [Fact]
    public void Counts_likely_cancellation_when_sprint_ends_in_combat()
    {
        var sprinting = State();
        sprinting.Player!.Statuses.Add(new StatusSnapshot { Name = "Sprint" });
        var combat = State();
        combat.Target = new TargetSnapshot { Hp = 50000, MaxHp = 58500, Distance = 10f };

        var analysis = ReplayAnalyzer.Analyze(Frames(sprinting, combat));

        Assert.Equal(1, analysis.SprintLikelyCancellationCount);
    }

    private static IReadOnlyList<RecordedGameState> Frames(GameState first, GameState second) =>
    [
        new() { CapturedAtUtc = DateTime.UnixEpoch, State = first },
        new() { CapturedAtUtc = DateTime.UnixEpoch.AddSeconds(2), State = second }
    ];

    private static GameState State()
    {
        var state = new GameState
        {
            LoggedIn = true,
            TerritoryId = 1116,
            NearbyScanComplete = true,
            Player = new PlayerSnapshot { ObjectId = 1, Hp = 50000, MaxHp = 58500 }
        };
        state.Player.Actions.Add(new ActionCooldownSnapshot { Name = "Sprint", IsAvailable = true });
        return state;
    }
}
