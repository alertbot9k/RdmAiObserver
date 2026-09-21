using SamplePlugin;
using Xunit;

namespace RdmAiObserver.Tests;

public sealed class ScenarioTests
{
    public static IEnumerable<object[]> Cases => Enumerable.Range(0, ScenarioLibrary.Count)
        .Select(index => new object[] { ScenarioLibrary.Get(index) });

    [Theory]
    [MemberData(nameof(Cases))]
    public void Recommendation_matches_offline_scenario(Scenario scenario)
    {
        var actual = DecisionEngine.Evaluate(scenario.State, scenario.Trend);
        Assert.Equal(scenario.ExpectedRecommendation, actual.Recommendation);
    }

    [Fact]
    public void Existing_infrastructure_checks_pass()
    {
        var result = ScenarioLibrary.ValidateAll();
        Assert.Equal(57, result.Total);
        Assert.True(result.Failures.Count == 0, string.Join(Environment.NewLine, result.Failures));
    }

    [Fact]
    public void Mode_detection_requires_confirmed_evidence()
    {
        var state = new GameState { PvpUiActive = true };
        state.Player = new PlayerSnapshot { Hp = 1, MaxHp = 1 };
        Assert.Equal(ObservedPvpMode.Unknown, PvpModeDetector.Detect(state));

        for (var index = 0; index < 5; index++)
            state.Party.Add(new PartyMemberSnapshot { Name = $"Member {index}", Hp = 1, MaxHp = 1 });
        Assert.Equal(ObservedPvpMode.CrystallineConflict, PvpModeDetector.Detect(state));
    }

    [Fact]
    public void Mode_detection_recognizes_confirmed_live_match_territory_without_transient_ui_evidence()
    {
        var state = new GameState { TerritoryId = 1034 };

        Assert.Equal(ObservedPvpMode.CrystallineConflict, PvpModeDetector.Detect(state));
    }

    [Fact]
    public void Mode_detection_recognizes_cloud_nine_territory()
    {
        Assert.Equal(ObservedPvpMode.CrystallineConflict,
            PvpModeDetector.Detect(new GameState { TerritoryId = 1032 }));
    }

    [Fact]
    public void Mode_detection_recognizes_second_confirmed_live_match_territory()
    {
        var state = new GameState { TerritoryId = 1116 };

        Assert.Equal(ObservedPvpMode.CrystallineConflict, PvpModeDetector.Detect(state));
    }

    [Fact]
    public void Mode_detection_recognizes_confirmed_crystal_with_incomplete_party()
    {
        var state = new GameState
        {
            PvpUiActive = true,
            Objective = new ObjectiveSnapshot { BaseId = ObjectiveEvaluator.TacticalCrystalBaseId }
        };

        Assert.Equal(ObservedPvpMode.CrystallineConflict, PvpModeDetector.Detect(state));
    }

    [Fact]
    public void Empty_replay_has_no_activity()
    {
        var report = ReplayAnalyzer.CreateReport([], DateTime.UnixEpoch);
        Assert.Empty(report.Timeline);
        Assert.Equal(0, report.Analysis.SnapshotCount);
    }
}
