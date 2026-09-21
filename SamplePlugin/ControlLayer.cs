using System;
using System.Collections.Generic;

namespace SamplePlugin;

public enum ControlCommandKind { Action, SelectTarget, Move, CancelCast, Stop }
public enum ControlResult { NotExecuted, Simulated, Rejected, Verified }

public sealed record ControlCommand(
    ControlCommandKind Kind,
    string Description,
    string? ActionName = null,
    ulong TargetObjectId = 0,
    float? X = null,
    float? Y = null,
    float? Z = null,
    TimeSpan? Timeout = null);

public sealed record ControlReceipt(
    ControlCommand Command,
    ControlResult Result,
    string Reason,
    DateTime IssuedAtUtc);

public interface IControlExecutor
{
    bool IsEmergencyStopped { get; }
    ControlReceipt Execute(ControlCommand command, ObservationFacts before);
    void EmergencyStop(string reason);
}

/// <summary>
/// Safe executor used by tests and the observer. It never calls game input APIs.
/// </summary>
public sealed class DryRunControlExecutor : IControlExecutor
{
    private readonly List<ControlReceipt> receipts = new();
    public bool IsEmergencyStopped { get; private set; }
    public IReadOnlyList<ControlReceipt> Receipts => receipts;

    public ControlReceipt Execute(ControlCommand command, ObservationFacts before)
    {
        if (IsEmergencyStopped)
            return Record(command, ControlResult.NotExecuted, "Emergency stop is active.");
        if (!before.CanRecommend)
            return Record(command, ControlResult.Rejected, "Observation is stale or incomplete.");
        if (command.Kind == ControlCommandKind.Stop)
        {
            EmergencyStop(command.Description);
            return receipts[^1];
        }
        return Record(command, ControlResult.Simulated, "Dry-run only; no game input was sent.");
    }

    public void EmergencyStop(string reason)
    {
        IsEmergencyStopped = true;
        Record(new ControlCommand(ControlCommandKind.Stop, reason), ControlResult.Verified, reason);
    }

    private ControlReceipt Record(ControlCommand command, ControlResult result, string reason)
    {
        var receipt = new ControlReceipt(command, result, reason, DateTime.UtcNow);
        receipts.Add(receipt);
        return receipt;
    }
}
