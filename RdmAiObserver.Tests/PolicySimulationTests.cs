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

    [Fact]
    public void Simulation_abstains_from_actions_against_invulnerable_targets()
    {
        var state = new GameState
        {
            TerritoryId = 1293, NearbyScanRadius = 60f, NearbyScanComplete = true,
            Player = new PlayerSnapshot { Hp = 50000, MaxHp = 58500 },
            Target = new TargetSnapshot { Name = "Enemy", Hp = 50000, MaxHp = 58500, Distance = 10f,
                Statuses = new List<StatusSnapshot> { new() { Name = "Invincibility" } } }
        };
        var result = PolicySimulator.Run(
            [new RecordedGameState { CapturedAtUtc = DateTime.UnixEpoch, State = state }],
            new SafetyPolicy(new HashSet<string> { "Use Prefulgence", "Target Enemy" }), DateTime.UnixEpoch);

        Assert.Equal(1, result.ObserveOnlySnapshots);
        Assert.Equal(0, result.SimulatedCommands);
    }

    [Fact]
    public void Simulation_counts_commands_that_miss_their_timing_window()
    {
        var state = new GameState
        {
            TerritoryId = 1293, NearbyScanRadius = 60f, NearbyScanComplete = true,
            Player = new PlayerSnapshot { Hp = 50000, MaxHp = 58500 },
            Target = new TargetSnapshot { Name = "Enemy", Hp = 50000, MaxHp = 58500, Distance = 10f }
        };
        var result = PolicySimulator.Run(
            [new RecordedGameState { CapturedAtUtc = DateTime.UnixEpoch, State = state }],
            new SafetyPolicy(new HashSet<string> { "Target Enemy" }), DateTime.UnixEpoch,
            new PolicySimulator.Options(TimeSpan.FromSeconds(1)));

        Assert.True(result.TimingFailures >= 1);
        Assert.True(result.MaxFailureStreak >= 1);
    }

    [Fact]
    public void Simulation_emergency_stops_after_repeated_timing_failures()
    {
        var state = new GameState { TerritoryId = 1293, NearbyScanRadius = 60f, NearbyScanComplete = true,
            Player = new PlayerSnapshot { Hp = 50000, MaxHp = 58500 }, Target = new TargetSnapshot { Name = "Enemy", Hp = 50000, MaxHp = 58500, Distance = 10f } };
        var recordings = new[] { 0, 1, 2 }.Select(i => new RecordedGameState { CapturedAtUtc = DateTime.UnixEpoch.AddSeconds(i), State = state }).ToArray();
        var result = PolicySimulator.Run(recordings, new SafetyPolicy(new HashSet<string> { "Target Enemy" }), DateTime.UnixEpoch,
            new PolicySimulator.Options(TimeSpan.FromSeconds(1), 2));

        Assert.True(result.EmergencyStops >= 1);
    }

    [Fact]
    public void Simulation_detects_accepted_action_without_observable_effect()
    {
        var state = new GameState { TerritoryId = 1293, NearbyScanRadius = 60f, NearbyScanComplete = true,
            Player = new PlayerSnapshot { Hp = 10000, MaxHp = 58500, Mp = 2000 } };
        var recordings = new[] { new RecordedGameState { CapturedAtUtc = DateTime.UnixEpoch, State = state },
            new RecordedGameState { CapturedAtUtc = DateTime.UnixEpoch.AddSeconds(1), State = state } };
        var result = PolicySimulator.Run(recordings, new SafetyPolicy(new HashSet<string> { "Recuperate" }), DateTime.UnixEpoch);

        Assert.True(result.VerificationFailures >= 1);
    }
}
