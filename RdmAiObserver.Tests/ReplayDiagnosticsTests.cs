using SamplePlugin;
using Xunit;

namespace RdmAiObserver.Tests;

public sealed class ReplayDiagnosticsTests
{
    [Fact]
    public void Diagnostics_explain_missing_crystal_and_sprint_evidence()
    {
        var frames = new[] { Frame(DateTime.UnixEpoch, State()) };
        var diagnostics = RecordingDiagnostics.Analyze(frames);
        Assert.Contains(diagnostics, issue => issue.Code == "missing-crystal");
        Assert.Contains(diagnostics, issue => issue.Code == "missing-sprint");
    }

    [Fact]
    public void Event_tracker_counts_lifecycle_target_and_engagement_transitions()
    {
        var protectedState = State();
        protectedState.Player!.Statuses.Add(new StatusSnapshot { Name = "Invincibility" });
        var engaged = State();
        engaged.Target = new TargetSnapshot { ObjectId = 10, Name = "One", Hp = 1, MaxHp = 1, Distance = 10 };
        var changed = State();
        changed.Target = new TargetSnapshot { ObjectId = 11, Name = "Two", Hp = 1, MaxHp = 1, Distance = 10 };
        var dead = State(); dead.Player!.Hp = 0;
        var respawn = State(); respawn.Player!.Statuses.Add(new StatusSnapshot { Name = "Invincibility" });
        var events = MatchEventTracker.Analyze(new[] { Frame(0, protectedState), Frame(2, engaged), Frame(4, changed), Frame(6, dead), Frame(8, respawn) });
        Assert.Equal(1, events.Deaths);
        Assert.Equal(1, events.Respawns);
        Assert.Equal(1, events.TargetChanges);
        Assert.Equal(1, events.EngagementStarts);
        Assert.Equal(1, events.EngagementEnds);
        Assert.Equal(1, events.SpawnProtectionEntries);
        Assert.Equal(1, events.SpawnProtectionExits);
    }

    [Fact]
    public void Consistency_analyzer_flags_same_target_rapid_reversal()
    {
        var a = State(); a.Target = Target(10); a.Player!.Actions.Add(new ActionCooldownSnapshot { Name = "Resolution", IsAvailable = true });
        var b = State(); b.Target = Target(10); b.Player!.Actions.Add(new ActionCooldownSnapshot { Name = "Resolution", IsCoolingDown = true });
        var c = State(); c.Target = Target(10); c.Player!.Actions.Add(new ActionCooldownSnapshot { Name = "Resolution", IsAvailable = true });
        var issues = RecommendationConsistencyAnalyzer.Analyze(new[] { Frame(0, a), Frame(2, b), Frame(4, c) });
        Assert.Contains(issues, issue => issue.Kind == "RapidReversal");
    }

    [Fact]
    public void Diagnostics_detect_frozen_capture_sequence()
    {
        var state = State();
        state.Target = Target(10);
        state.Player!.Statuses.Add(new StatusSnapshot { Id = 1, Name = "Guard", RemainingSeconds = 4 });
        var frames = Enumerable.Range(0, 6).Select(index => Frame(index * 2, state)).ToArray();
        Assert.Contains(RecordingDiagnostics.Analyze(frames), issue => issue.Code == "frozen-capture");
    }

    [Fact]
    public void Replay_report_records_lifecycle_transitions()
    {
        var outside = State(); outside.TerritoryId = 1310;
        var countdown = State(); countdown.Player!.Statuses.Add(new StatusSnapshot { Name = "Invincibility" });
        var active = State();
        var results = State(); results.PvpUiActive = true;
        var exited = State(); exited.TerritoryId = 1310;
        var report = ReplayAnalyzer.CreateReport(new[] { Frame(0, outside), Frame(2, countdown), Frame(4, active), Frame(6, results), Frame(8, exited) }, DateTime.UnixEpoch);
        Assert.Equal(new[] { CcLifecycleState.OutsideCc, CcLifecycleState.Countdown, CcLifecycleState.Active, CcLifecycleState.Results, CcLifecycleState.Exited },
            report.LifecycleTransitions.Select(item => item.State));
    }

    private static TargetSnapshot Target(float distance) => new() { ObjectId = 10, Name = "Enemy", Hp = 50000, MaxHp = 58500, Distance = distance };
    private static RecordedGameState Frame(int seconds, GameState state) => Frame(DateTime.UnixEpoch.AddSeconds(seconds), state);
    private static RecordedGameState Frame(DateTime at, GameState state) => new() { CapturedAtUtc = at, State = state };
    private static GameState State() => new() { TerritoryId = 1116, NearbyScanComplete = true,
        Player = new PlayerSnapshot { ObjectId = 1, Hp = 50000, MaxHp = 58500 } };
}
