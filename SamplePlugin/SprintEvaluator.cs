using System;
using System.Collections.Generic;
using System.Linq;

namespace SamplePlugin;

/// <summary>Produces separate read-only PvP Sprint advice.</summary>
public static class SprintEvaluator
{
    private const float LongTravelDistance = 25f;

    public static SprintRecommendation Evaluate(GameState state)
    {
        if (PvpModeDetector.Detect(state) != ObservedPvpMode.CrystallineConflict)
            return new("No Sprint advice", "The snapshot is not confirmed as Crystalline Conflict.");

        var player = state.Player;
        if (player == null || player.Hp == 0)
            return new("Hold Sprint", "Wait until the player is alive and controllable.");

        if (HasStatus(player.Statuses, "Sprint"))
            return new("Keep Sprint active", "Sprint is active; avoid unnecessary actions while travelling because another action ends PvP Sprint.");

        var readiness = GetReadiness(player.Actions, "Sprint");
        if (readiness != ActionReadiness.Ready)
            return new("Hold Sprint", readiness == ActionReadiness.Unknown
                ? "Sprint availability was not captured." : "Sprint is not currently available.");

        var enemies = CombatProximity.CountEnemies(state, 15f);
        var allies = CombatProximity.CountAllies(state, 15f);
        if (enemies >= 2 && allies == 0)
            return new("Use Sprint to disengage", $"{enemies} opponents are nearby and no living ally is within 15 yalms.");

        if (enemies == 0 && state.Objective?.DistanceToPlayer >= LongTravelDistance)
            return new("Use Sprint toward the crystal", $"The crystal is {state.Objective.DistanceToPlayer:F0} yalms away and no opponent is within 15 yalms.");

        if (enemies == 0 && state.Target == null)
            return new("Use Sprint to regroup", "No opponent is nearby or targeted; use the travel window to rejoin the fight.");

        return new("Hold Sprint", "Combat is in immediate range; an offensive or defensive action would end PvP Sprint.");
    }

    private static bool HasStatus(IEnumerable<StatusSnapshot> statuses, string name) =>
        statuses.Any(status => string.Equals(status.Name, name, StringComparison.OrdinalIgnoreCase));

    private static ActionReadiness GetReadiness(IEnumerable<ActionCooldownSnapshot> actions, string name)
    {
        var action = actions.FirstOrDefault(action => string.Equals(action.Name, name, StringComparison.OrdinalIgnoreCase));
        if (action == null) return ActionReadiness.Unknown;
        return action.IsAvailable ? ActionReadiness.Ready
            : action.IsCoolingDown ? ActionReadiness.CoolingDown : ActionReadiness.Unavailable;
    }
}

public sealed record SprintRecommendation(string Recommendation, string Reason);
