using System;
using System.Collections.Generic;

namespace SamplePlugin;

public enum CommandVerificationResult { Verified, Failed, NotObservable }

public sealed record CommandVerification(
    ControlCommand Command,
    CommandVerificationResult Result,
    string Reason,
    DateTime IssuedAtUtc,
    DateTime ObservedAtUtc);

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
    int VerifiedCommands,
    int VerificationFailures,
    int UnverifiableCommands,
    IReadOnlyList<ControlReceipt> Receipts,
    IReadOnlyList<CommandVerification> Verifications)
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
        var verifiedCommands = 0;
        var verificationFailures = 0;
        var unverifiableCommands = 0;
        var verifications = new List<CommandVerification>();
        var pendingCommands = new List<(ControlCommand Command, ObservationFacts Facts, DateTime IssuedAtUtc)>();
        var latency = options?.CommandLatency ?? TimeSpan.Zero;
        var maxFailureLimit = options?.MaxFailureStreak ?? 3;
        var wasActionable = false;
        var now = startTimeUtc ?? DateTime.UtcNow;

        foreach (var recording in recordings)
        {
            var facts = ObservationFacts.From(recording.State, recording.CapturedAtUtc, recording.CapturedAtUtc);
            foreach (var pending in pendingCommands)
            {
                var result = VerifyTransition(pending.Command, pending.Facts, facts, out var reason);
                if (result == CommandVerificationResult.Verified) verifiedCommands++;
                else if (result == CommandVerificationResult.Failed) verificationFailures++;
                else unverifiableCommands++;
                verifications.Add(new CommandVerification(
                    pending.Command, result, reason, pending.IssuedAtUtc, recording.CapturedAtUtc));
            }
            pendingCommands.Clear();
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
            if (safety.Mode != SafetyMode.EmergencyStopped)
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
                if (receipt.Result == ControlResult.Simulated)
                    pendingCommands.Add((command, facts, recording.CapturedAtUtc));
            }
            now = recording.CapturedAtUtc;
        }

        return new(recordings.Count, planned, observeOnly, simulated, rejected, recoveries, emergencyStops, safetyFallbacks, gameRejected, timingFailures, maxFailureStreak, verifiedCommands, verificationFailures, unverifiableCommands, receipts, verifications);
    }

    private static ControlCommand ToCommand(PolicyStep step)
    {
        return new ControlCommand(step.Kind, step.Purpose, step.ActionName, step.TargetObjectId,
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
        if (command.Kind == ControlCommandKind.SelectTarget && command.TargetObjectId == 0)
        { reason = "No stable target identity was available."; return false; }
        if (command.Kind == ControlCommandKind.CancelCast && facts.State.Player?.Cast == null)
        { reason = "There is no observed cast to cancel."; return false; }
        reason = "Simulated game accepted the command.";
        return true;
    }

    private static CommandVerificationResult VerifyTransition(
        ControlCommand command,
        ObservationFacts before,
        ObservationFacts after,
        out string reason)
    {
        if (command.ActionName == "Recuperate")
            return Observed(
                after.State.Player is { Hp: > 0 } player && before.State.Player is { Hp: > 0 } old &&
                (player.Hp > old.Hp || player.Mp < old.Mp),
                "HP increased or MP decreased after Recuperate.",
                "Neither an HP increase nor an MP decrease was observed after Recuperate.", out reason);
        if (command.ActionName == "Purify")
            return Observed(before.PlayerCrowdControlled && !after.PlayerCrowdControlled,
                "Observed crowd control cleared after Purify.",
                "Crowd control was not observed clearing after Purify.", out reason);
        if (command.ActionName == "Guard")
            return Observed(after.PlayerMitigation == MitigationState.Guarding,
                "Observed Guard mitigation after the command.",
                "Guard mitigation was not observed after the command.", out reason);
        if (command.ActionName == "Standard-issue Elixir")
            return Observed(
                after.State.Player is { Hp: > 0 } player && before.State.Player is { Hp: > 0 } old &&
                (player.Hp > old.Hp || player.Mp > old.Mp),
                "Observed HP or MP recovery after Standard-issue Elixir.",
                "No HP or MP recovery was observed after Standard-issue Elixir.", out reason);
        if (command.Kind == ControlCommandKind.Move)
            return Observed(before.State.Player != null && after.State.Player != null &&
                (before.State.Player.X != after.State.Player.X || before.State.Player.Y != after.State.Player.Y || before.State.Player.Z != after.State.Player.Z),
                "Observed player position change after movement advice.",
                "Player position did not change before the next snapshot.", out reason);
        if (command.Kind == ControlCommandKind.SelectTarget)
            return Observed(after.State.Target?.ObjectId == command.TargetObjectId,
                "Observed the requested target identity.",
                "The requested target identity was not selected in the next snapshot.", out reason);
        if (command.Kind == ControlCommandKind.CancelCast)
            return Observed(before.State.Player?.Cast != null && after.State.Player?.Cast == null,
                "Observed the cast end after cancellation advice.",
                "The cast was still present in the next snapshot.", out reason);

        reason = "No reliable next-snapshot effect is defined for this command.";
        return CommandVerificationResult.NotObservable;
    }

    private static CommandVerificationResult Observed(
        bool condition, string success, string failure, out string reason)
    {
        reason = condition ? success : failure;
        return condition ? CommandVerificationResult.Verified : CommandVerificationResult.Failed;
    }
}
