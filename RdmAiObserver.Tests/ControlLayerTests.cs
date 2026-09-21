using SamplePlugin;
using Xunit;

namespace RdmAiObserver.Tests;

public sealed class ControlLayerTests
{
    [Fact]
    public void Dry_run_executor_never_executes_game_input()
    {
        var state = new GameState { TerritoryId = 1293, NearbyScanRadius = 60f, NearbyScanComplete = true,
            Player = new PlayerSnapshot { Hp = 50000, MaxHp = 58500 } };
        var facts = ObservationFacts.From(state, DateTime.UnixEpoch);
        var executor = new DryRunControlExecutor();

        var receipt = executor.Execute(new ControlCommand(ControlCommandKind.Action, "Use Recuperate", "Recuperate"), facts);

        Assert.Equal(ControlResult.Simulated, receipt.Result);
        Assert.Contains("Dry-run", receipt.Reason);
    }

    [Fact]
    public void Emergency_stop_rejects_subsequent_commands()
    {
        var state = new GameState { TerritoryId = 1293, NearbyScanRadius = 60f, NearbyScanComplete = true,
            Player = new PlayerSnapshot { Hp = 50000, MaxHp = 58500 } };
        var executor = new DryRunControlExecutor();
        executor.EmergencyStop("operator stop");

        var receipt = executor.Execute(new ControlCommand(ControlCommandKind.Move, "Move to safety"), ObservationFacts.From(state, DateTime.UnixEpoch));

        Assert.True(executor.IsEmergencyStopped);
        Assert.Equal(ControlResult.NotExecuted, receipt.Result);
    }
}
