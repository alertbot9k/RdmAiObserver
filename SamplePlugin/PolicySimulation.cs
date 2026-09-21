using System;
using System.Collections.Generic;

namespace SamplePlugin;

public sealed record PolicySimulationResult(
    int SnapshotCount,
    int PlannedSnapshots,
    int ObserveOnlySnapshots,
    int SimulatedCommands,
    int RejectedCommands,
    int RecoveryTransitions,
    int EmergencyStops,
    IReadOnlyList<ControlReceipt> Receipts)
{
    public float AcceptancePercent => SimulatedCommands + RejectedCommands == 0
        ? 0f : SimulatedCommands * 100f / (SimulatedCommands + RejectedCommands);
}

/// <summary>Runs policy and safety decisions against recordings without game input.</summary>
public static class PolicySimulator
{
    public static PolicySimulationResult Run(
        IReadOnlyList<RecordedGameState> recordings,
        SafetyPolicy policy,
        DateTime? startTimeUtc = null)
    {
        var safety = new OperationalSafety(policy);
        var executor = new DryRunControlExecutor();
        var receipts = new List<ControlReceipt>();
        var planned = 0;
        var observeOnly = 0;
        var simulated = 0;
        var rejected = 0;
        var recoveries = 0;
        var emergencyStops = 0;
        var wasActionable = false;
        var now = startTimeUtc ?? DateTime.UtcNow;

        foreach (var recording in recordings)
        {
            var facts = ObservationFacts.From(recording.State, recording.CapturedAtUtc, recording.CapturedAtUtc);
            var plan = PolicyPlanner.Plan(facts);
            if (plan.Status == PolicyPlanStatus.ObserveOnly)
            {
                observeOnly++;
                if (wasActionable) recoveries++;
                wasActionable = false;
                continue;
            }

            planned++;
            wasActionable = true;
            safety.Enable(recording.CapturedAtUtc);
            foreach (var step in plan.Steps)
            {
                var command = ToCommand(step);
                if (!safety.TryAuthorize(command, facts, now, out var reason))
                {
                    rejected++;
                    receipts.Add(new ControlReceipt(command, ControlResult.Rejected, reason, now));
                    continue;
                }

                var receipt = executor.Execute(command, facts);
                receipts.Add(receipt);
                if (receipt.Result == ControlResult.Simulated) simulated++;
                else if (receipt.Result == ControlResult.Rejected) rejected++;
            }
            now = recording.CapturedAtUtc;
        }

        return new(recordings.Count, planned, observeOnly, simulated, rejected, recoveries, emergencyStops, receipts);
    }

    private static ControlCommand ToCommand(PolicyStep step)
    {
        var action = step.Action.StartsWith("Use ", StringComparison.Ordinal)
            ? step.Action[4..]
            : step.Action;
        return new ControlCommand(ControlCommandKind.Action, step.Purpose, action);
    }
}
