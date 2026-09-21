using SamplePlugin;
using Xunit;

namespace RdmAiObserver.Tests;

public sealed class MatchLifecycleTrackerTests
{
    [Fact]
    public void Reports_outside_cc_without_confirmed_evidence()
    {
        var tracker = new MatchLifecycleTracker();
        Assert.Equal(CcLifecycleState.OutsideCc, tracker.Update(new GameState()).State);
    }

    [Fact]
    public void Reports_loading_before_player_is_available()
    {
        var tracker = new MatchLifecycleTracker();
        Assert.Equal(CcLifecycleState.Loading, tracker.Update(new GameState { TerritoryId = 1116 }).State);
    }

    [Fact]
    public void Transitions_from_countdown_to_active()
    {
        var tracker = new MatchLifecycleTracker();
        var countdown = CcState();
        countdown.Player!.Statuses.Add(new StatusSnapshot { Name = "Invincibility" });

        Assert.Equal(CcLifecycleState.Countdown, tracker.Update(countdown).State);
        Assert.Equal(CcLifecycleState.Active, tracker.Update(CcState()).State);
    }

    [Fact]
    public void Keeps_respawn_state_until_spawn_protection_ends()
    {
        var tracker = new MatchLifecycleTracker();
        Assert.Equal(CcLifecycleState.Active, tracker.Update(CcState()).State);

        var dead = CcState();
        dead.Player!.Hp = 0;
        Assert.Equal(CcLifecycleState.DeadRespawning, tracker.Update(dead).State);

        var protectedRespawn = CcState();
        protectedRespawn.Player!.Statuses.Add(new StatusSnapshot { Name = "Invincibility" });
        Assert.Equal(CcLifecycleState.DeadRespawning, tracker.Update(protectedRespawn).State);
        Assert.Equal(CcLifecycleState.Active, tracker.Update(CcState()).State);
    }

    [Fact]
    public void Reports_results_only_after_active_play()
    {
        var tracker = new MatchLifecycleTracker();
        Assert.Equal(CcLifecycleState.Active, tracker.Update(CcState()).State);
        var results = CcState();
        results.PvpUiActive = true;
        Assert.Equal(CcLifecycleState.Results, tracker.Update(results).State);
    }

    [Fact]
    public void Emits_exited_once_then_outside()
    {
        var tracker = new MatchLifecycleTracker();
        tracker.Update(CcState());
        Assert.Equal(CcLifecycleState.Exited, tracker.Update(new GameState()).State);
        Assert.Equal(CcLifecycleState.OutsideCc, tracker.Update(new GameState()).State);
    }

    private static GameState CcState() => new()
    {
        LoggedIn = true,
        TerritoryId = 1116,
        Player = new PlayerSnapshot { ObjectId = 1, Hp = 58500, MaxHp = 58500 }
    };
}
