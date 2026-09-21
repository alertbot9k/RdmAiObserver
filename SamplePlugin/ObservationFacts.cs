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

public enum ObservationCompleteness
{
    Unknown,
    Partial,
    Complete
}

public enum LineOfSightState { Unknown, Clear, Blocked }
public enum ComboEvidence { None, Started, MidChain, Completed }
public enum MitigationState { None, Guarding, Invulnerable }
public enum ModeStrategy { Unknown, CrystallineConflict }

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
    ObservationCompleteness NearbyCompleteness,
    ObservationEvidence HostilityEvidence,
    ComboEvidence Combo,
    MitigationState PlayerMitigation,
    MitigationState TargetMitigation,
    bool PlayerCrowdControlled,
    bool TargetCrowdControlled,
    bool TargetCastInterruptible,
    LineOfSightState LineOfSight,
    ModeStrategy Strategy,
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

    public bool CanRecommend => IsFresh && HasPlayer;

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
        var completeness = state.NearbyScanComplete
            ? ObservationCompleteness.Complete
            : state.NearbyScanRadius > 0 ? ObservationCompleteness.Partial : ObservationCompleteness.Unknown;
        var hostilityEvidence = state.NearbyCharacters.Count == 0
            ? ObservationEvidence.Unknown
            : state.NearbyCharacters.All(character => character.Relation != CombatRelation.Unknown)
                ? ObservationEvidence.Confirmed
                : ObservationEvidence.Observed;
        var combo = HasStatus(player?.Statuses, "Enchanted Redoublement")
            ? ComboEvidence.Completed
            : HasStatus(player?.Statuses, "Enchanted Zwerchhau") || HasStatus(player?.Statuses, "Enchanted Riposte")
                ? ComboEvidence.MidChain
                : HasStatus(player?.Statuses, "Dualcast") ? ComboEvidence.Started : ComboEvidence.None;
        var playerMitigation = HasStatus(player?.Statuses, "Invincibility")
            ? MitigationState.Invulnerable : HasStatus(player?.Statuses, "Guard") ? MitigationState.Guarding : MitigationState.None;
        var targetMitigation = HasStatus(target?.Statuses, "Invincibility")
            ? MitigationState.Invulnerable : HasStatus(target?.Statuses, "Guard") ? MitigationState.Guarding : MitigationState.None;
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
            completeness,
            hostilityEvidence,
            combo,
            playerMitigation,
            targetMitigation,
            FindCrowdControl(player?.Statuses),
            FindCrowdControl(target?.Statuses),
            target?.Cast?.IsInterruptible == true,
            LineOfSightState.Unknown,
            mode == ObservedPvpMode.CrystallineConflict ? ModeStrategy.CrystallineConflict : ModeStrategy.Unknown,
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
            CombatProximity.CountAllies(state, 15f),
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

    private static bool HasStatus(IEnumerable<StatusSnapshot>? statuses, string name) =>
        statuses?.Any(status => string.Equals(status.Name, name, StringComparison.OrdinalIgnoreCase)) == true;

    private static bool FindCrowdControl(IEnumerable<StatusSnapshot>? statuses) =>
        statuses?.Any(status => status.Name is "Stun" or "Heavy" or "Bind" or "Silence" or "Deep Freeze" or "Miracle of Nature") == true;
}
