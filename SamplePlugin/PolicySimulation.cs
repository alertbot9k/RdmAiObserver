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
    int SafetyFallbacks,
    int GameRejectedCommands,
    int TimingFailures,
    int MaxFailureStreak,
    IReadOnlyList<ControlReceipt> Receipts)
{
    public float AcceptancePercent => SimulatedCommands + RejectedCommands == 0
        ? 0f : SimulatedCommands * 100f / (SimulatedCommands + RejectedCommands);
}

/// <summary>Runs policy and safety decisions against recordings without game input.</summary>
public static class PolicySimulator
{
    public sealed record Options(TimeSpan? CommandLatency = null, int MaxFailureStreak = 3);

    public static PolicySimulationResult Run(
        IReadOnlyList<RecordedGameState> recordings,
        SafetyPolicy policy,
        DateTime? startTimeUtc = null,
        Options? options = null)
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
        var safetyFallbacks = 0;
        var gameRejected = 0;
        var timingFailures = 0;
        var failureStreak = 0;
        var maxFailureStreak = 0;
        var latency = options?.CommandLatency ?? TimeSpan.Zero;
        var maxFailureLimit = options?.MaxFailureStreak ?? 3;
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
                if (safety.Mode == SafetyMode.Enabled)
                {
                    safetyFallbacks++;
                    safety.Disable("Simulation entered observation-only mode.");
                }
                wasActionable = false;
                continue;
            }

            if (plan.Status == PolicyPlanStatus.Recover)
            {
                recoveries++;
                wasActionable = false;
                continue;
            }

            planned++;
            wasActionable = true;
            safety.Enable(recording.CapturedAtUtc);
            foreach (var step in plan.Steps)
            {
                var command = ToCommand(step);
                if (command.Timeout is { } timeout && latency > timeout)
                {
                    timingFailures++;
                    failureStreak++;
                    maxFailureStreak = Math.Max(maxFailureStreak, failureStreak);
                    if (failureStreak >= maxFailureLimit)
                    { safety.EmergencyStop("Repeated timing failures in simulation."); emergencyStops++; }
                    receipts.Add(new ControlReceipt(command, ControlResult.Rejected, "Simulated command latency exceeded its timeout window.", now));
                    recoveries++;
                    continue;
                }
                if (!safety.TryAuthorize(command, facts, now, out var reason))
                {
                    rejected++;
                    failureStreak++;
                    maxFailureStreak = Math.Max(maxFailureStreak, failureStreak);
                    if (failureStreak >= maxFailureLimit)
                    { safety.EmergencyStop("Repeated safety rejections in simulation."); emergencyStops++; }
                    receipts.Add(new ControlReceipt(command, ControlResult.Rejected, reason, now));
                    continue;
                }

                if (!GameWouldAccept(command, facts, out var gameReason))
                {
                    gameRejected++;
                    failureStreak++;
                    maxFailureStreak = Math.Max(maxFailureStreak, failureStreak);
                    if (failureStreak >= maxFailureLimit)
                    { safety.EmergencyStop("Repeated game rejections in simulation."); emergencyStops++; }
                    receipts.Add(new ControlReceipt(command, ControlResult.Rejected, gameReason, now));
                    recoveries++;
                    continue;
                }

                var receipt = executor.Execute(command, facts);
                receipts.Add(receipt);
                if (receipt.Result == ControlResult.Simulated) simulated++;
                else if (receipt.Result == ControlResult.Rejected) rejected++;
                if (receipt.Result == ControlResult.Simulated) failureStreak = 0;
            }
            now = recording.CapturedAtUtc;
        }

        return new(recordings.Count, planned, observeOnly, simulated, rejected, recoveries, emergencyStops, safetyFallbacks, gameRejected, timingFailures, maxFailureStreak, receipts);
    }

    private static ControlCommand ToCommand(PolicyStep step)
    {
        var action = step.Action.StartsWith("Use ", StringComparison.Ordinal)
            ? step.Action[4..]
            : step.Action;
        return new ControlCommand(ControlCommandKind.Action, step.Purpose, action,
            Timeout: step.Interruptible ? TimeSpan.FromMilliseconds(500) : null);
    }

    private static bool GameWouldAccept(ControlCommand command, ObservationFacts facts, out string reason)
    {
        if (facts.MatchPhase is ObservedMatchPhase.Loading or ObservedMatchPhase.Respawning)
        { reason = "Game is not in an active phase."; return false; }
        if (command.Kind == ControlCommandKind.Action && facts.TargetMitigation == MitigationState.Invulnerable)
        { reason = "Target is invulnerable; simulated game rejected the action."; return false; }
        if (command.Kind == ControlCommandKind.Action && facts.LineOfSight == LineOfSightState.Blocked)
        { reason = "Line of sight is blocked; simulated game rejected the action."; return false; }
        reason = "Simulated game accepted the command.";
        return true;
    }
}
