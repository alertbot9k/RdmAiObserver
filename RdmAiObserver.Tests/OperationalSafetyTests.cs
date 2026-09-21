using SamplePlugin;
using Xunit;

namespace RdmAiObserver.Tests;

public sealed class OperationalSafetyTests
{
    private static ObservationFacts FreshFacts() => ObservationFacts.From(new GameState
    {
        TerritoryId = 1293, NearbyScanRadius = 60f, NearbyScanComplete = true,
        Player = new PlayerSnapshot { Hp = 50000, MaxHp = 58500 }
    }, DateTime.UnixEpoch);

    [Fact]
    public void Safety_starts_disabled_and_requires_explicit_enablement()
    {
        var safety = new OperationalSafety(new SafetyPolicy(new HashSet<string> { "Recuperate" }));
        var command = new ControlCommand(ControlCommandKind.Action, "Recover", "Recuperate");

        Assert.False(safety.TryAuthorize(command, FreshFacts(), DateTime.UnixEpoch, out _));
        safety.Enable(DateTime.UnixEpoch);
        Assert.True(safety.TryAuthorize(command, FreshFacts(), DateTime.UnixEpoch, out _));
    }

    [Fact]
    public void Safety_rejects_unlisted_actions_and_falls_back_on_stale_data()
    {
        var safety = new OperationalSafety(new SafetyPolicy(new HashSet<string> { "Recuperate" }));
        safety.Enable(DateTime.UnixEpoch);

        var unknown = new ControlCommand(ControlCommandKind.Action, "Unknown", "Unknown");
        Assert.False(safety.TryAuthorize(unknown, FreshFacts(), DateTime.UnixEpoch, out _));

        var stale = ObservationFacts.From(FreshFacts().State, DateTime.UnixEpoch, DateTime.UnixEpoch.AddSeconds(10));
        Assert.False(safety.TryAuthorize(new ControlCommand(ControlCommandKind.Move, "Move"), stale, DateTime.UnixEpoch.AddSeconds(10), out _));
        Assert.Equal(SafetyMode.ObservationOnly, safety.Mode);
    }
}
