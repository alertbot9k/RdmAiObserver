using SamplePlugin;
using Xunit;

namespace RdmAiObserver.Tests;

public sealed class PolicySimulationTests
{
    [Fact]
    public void Simulation_counts_plans_and_observe_only_recovery()
    {
        var active = new GameState
        {
            TerritoryId = 1293, NearbyScanRadius = 60f, NearbyScanComplete = true,
            Player = new PlayerSnapshot { Hp = 50000, MaxHp = 58500 },
            Target = new TargetSnapshot { Name = "Enemy", Hp = 50000, MaxHp = 58500, Distance = 10f }
        };
        var stale = new GameState { TerritoryId = 1293, Player = new PlayerSnapshot { Hp = 50000, MaxHp = 58500 } };
        var recordings = new[]
        {
            new RecordedGameState { CapturedAtUtc = DateTime.UnixEpoch, State = active },
            new RecordedGameState { CapturedAtUtc = DateTime.UnixEpoch.AddSeconds(10), State = stale }
        };

        var result = PolicySimulator.Run(recordings,
            new SafetyPolicy(new HashSet<string> { "Recuperate", "Use Prefulgence", "Target Enemy" }),
            DateTime.UnixEpoch);

        Assert.Equal(2, result.SnapshotCount);
        Assert.True(result.PlannedSnapshots >= 1);
        Assert.True(result.ObserveOnlySnapshots >= 1);
        Assert.True(result.RecoveryTransitions >= 1);
        Assert.True(result.SafetyFallbacks >= 1);
    }
}
