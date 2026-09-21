using System;
using System.Collections.Generic;

namespace SamplePlugin;

public enum SafetyMode { ObservationOnly, Enabled, EmergencyStopped }

public sealed record SafetyPolicy(
    IReadOnlySet<string> AllowedActions,
    int MaxCommandsPerWindow = 4,
    TimeSpan? RateWindow = null,
    TimeSpan? WatchdogTimeout = null)
{
    public TimeSpan EffectiveRateWindow => RateWindow ?? TimeSpan.FromSeconds(2);
    public TimeSpan EffectiveWatchdogTimeout => WatchdogTimeout ?? TimeSpan.FromSeconds(3);
}

/// <summary>Enforces human enablement and fail-safe execution limits.</summary>
public sealed class OperationalSafety
{
    private readonly SafetyPolicy policy;
    private readonly Queue<DateTime> commandTimes = new();
    private DateTime lastObservationUtc;
    public SafetyMode Mode { get; private set; } = SafetyMode.ObservationOnly;
    public string Status { get; private set; } = "Disabled until explicitly enabled.";

    public OperationalSafety(SafetyPolicy policy) => this.policy = policy;

    public void Enable(DateTime observationUtc)
    {
        lastObservationUtc = observationUtc;
        Mode = SafetyMode.Enabled;
        Status = "Enabled with safety limits.";
    }

    public void Disable(string reason = "Disabled by operator.")
    {
        Mode = SafetyMode.ObservationOnly;
        Status = reason;
    }

    public void EmergencyStop(string reason = "Emergency stop activated.")
    {
        Mode = SafetyMode.EmergencyStopped;
        Status = reason;
    }

    public bool TryAuthorize(ControlCommand command, ObservationFacts facts, DateTime nowUtc, out string reason)
    {
        if (Mode != SafetyMode.Enabled) { reason = Status; return false; }
        if (!facts.CanRecommend) { Disable("Observation became stale; returned to observation-only mode."); reason = Status; return false; }
        if (nowUtc - facts.CapturedAtUtc > policy.EffectiveWatchdogTimeout) { Disable("Watchdog expired; returned to observation-only mode."); reason = Status; return false; }
        if (command.Kind == ControlCommandKind.Action && (command.ActionName == null || !policy.AllowedActions.Contains(command.ActionName)))
        { reason = "Action is not on the strict allowlist."; return false; }
        while (commandTimes.Count > 0 && nowUtc - commandTimes.Peek() > policy.EffectiveRateWindow) commandTimes.Dequeue();
        if (commandTimes.Count >= policy.MaxCommandsPerWindow) { reason = "Rate limit exceeded."; return false; }
        commandTimes.Enqueue(nowUtc);
        lastObservationUtc = facts.CapturedAtUtc;
        reason = "Authorized.";
        return true;
    }
}
