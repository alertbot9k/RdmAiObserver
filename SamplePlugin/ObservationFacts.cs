using System;
using System.Collections.Generic;
using System.Linq;

namespace SamplePlugin;

/// <summary>How strongly a fact is supported by the captured snapshot.</summary>
public enum ObservationEvidence
{
    Unknown,
    Observed,
    Confirmed
}

public enum ActionReadiness
{
    Unknown,
    Unavailable,
    Ready,
    CoolingDown
}

public enum ObservedMatchPhase
{
    Unknown,
    Loading,
    Active,
    Respawning,
    Protected
}

/// <summary>
/// Platform independent facts derived from one raw capture. Decision rules can
/// consume this boundary without knowing how Dalamud produced the snapshot.
/// </summary>
public sealed record ObservationFacts(
    GameState State,
    ObservedPvpMode Mode,
    ObservationEvidence ModeEvidence,
    DateTime CapturedAtUtc,
    TimeSpan Age,
    ObservationEvidence Freshness,
    ObservedMatchPhase MatchPhase,
    bool HasPlayer,
    bool PlayerAlive,
    bool PlayerProtected,
    bool PlayerGuarding,
    bool TargetPresent,
    bool TargetAlive,
    bool TargetProtected,
    bool TargetGuarding,
    int EnemiesWithin15Yalms,
    int EnemiesWithin25Yalms,
    int AlliesWithin15Yalms,
    IReadOnlyDictionary<string, ActionReadiness> Actions)
{
    public bool IsFresh => Freshness == ObservationEvidence.Confirmed;

    public ActionReadiness Readiness(string actionName) =>
        Actions.TryGetValue(actionName, out var readiness) ? readiness : ActionReadiness.Unknown;

    public static ObservationFacts From(GameState state, DateTime capturedAtUtc, DateTime? observedAtUtc = null)
    {
        var now = observedAtUtc ?? capturedAtUtc;
        var age = now >= capturedAtUtc ? now - capturedAtUtc : TimeSpan.Zero;
        var player = state.Player;
        var target = state.Target;
        var mode = PvpModeDetector.Detect(state);
        var modeEvidence = mode == ObservedPvpMode.Unknown ? ObservationEvidence.Unknown : ObservationEvidence.Confirmed;
        var freshness = age <= TimeSpan.FromSeconds(5) ? ObservationEvidence.Confirmed : ObservationEvidence.Observed;
        var phase = player == null
            ? ObservedMatchPhase.Loading
            : player.Hp == 0
                ? ObservedMatchPhase.Respawning
                : HasStatus(player.Statuses, "Invincibility")
                    ? ObservedMatchPhase.Protected
                    : mode == ObservedPvpMode.Unknown
                        ? ObservedMatchPhase.Unknown
                        : ObservedMatchPhase.Active;

        return new ObservationFacts(
            state,
            mode,
            modeEvidence,
            capturedAtUtc,
            age,
            freshness,
            phase,
            player != null,
            player is { Hp: > 0 },
            HasStatus(player?.Statuses, "Invincibility"),
            HasStatus(player?.Statuses, "Guard"),
            target != null,
            target is { Hp: > 0, MaxHp: > 0 },
            HasStatus(target?.Statuses, "Invincibility"),
            HasStatus(target?.Statuses, "Guard"),
            CombatProximity.CountEnemies(state, 15f),
            CombatProximity.CountEnemies(state, 25f),
            CountNearbyAllies(state),
            CaptureReadiness(player?.Actions));
    }

    private static Dictionary<string, ActionReadiness> CaptureReadiness(IEnumerable<ActionCooldownSnapshot>? actions)
    {
        var result = new Dictionary<string, ActionReadiness>(StringComparer.OrdinalIgnoreCase);
        if (actions == null) return result;
        foreach (var action in actions.Where(action => !string.IsNullOrWhiteSpace(action.Name)))
            result[action.Name] = action.IsAvailable
                ? ActionReadiness.Ready
                : action.IsCoolingDown ? ActionReadiness.CoolingDown : ActionReadiness.Unavailable;
        return result;
    }

    private static int CountNearbyAllies(GameState state) =>
        state.Party.Count(member => member.Hp > 0 && member.MaxHp > 0 && member.Distance <= 15f);

    private static bool HasStatus(IEnumerable<StatusSnapshot>? statuses, string name) =>
        statuses?.Any(status => string.Equals(status.Name, name, StringComparison.OrdinalIgnoreCase)) == true;
}
