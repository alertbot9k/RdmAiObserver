using System;
using System.Collections.Generic;

namespace SamplePlugin;

/// <summary>
/// Produces explainable, read-only PvP advice from an observed GameState.
/// It never issues game commands or controls the player.
/// </summary>
public static class DecisionEngine
{
    private const uint CommonActionMpCost = 2000;
    private const float TargetSwitchHpAdvantage = 25f;

    private static readonly HashSet<string> PurifiableStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Stun", "Heavy", "Bind", "Silence", "Deep Freeze", "Miracle of Nature"
    };

    public static DecisionRecommendation Evaluate(GameState state)
        => Evaluate(state, null);

    public static DecisionRecommendation Evaluate(GameState state, CombatTrend? trend)
        => Evaluate(ObservationFacts.From(state, DateTime.UtcNow), trend);

    public static DecisionRecommendation Evaluate(ObservationFacts facts, CombatTrend? trend = null)
    {
        if (!facts.CanRecommend)
            return Recommend(DecisionPriority.Observe, "Wait for a fresh observation", "The captured combat state is stale or incomplete.");

        var state = facts.State;
        var player = state.Player;
        if (player == null || player.MaxHp == 0)
            return Recommend(DecisionPriority.Wait, "Wait for player data", "The player snapshot is incomplete.", RecommendationConfidence.Low);

        if (facts.Mode == ObservedPvpMode.Unknown)
            return Recommend(DecisionPriority.Observe, "Observe outside confirmed CC", "No confirmed Crystalline Conflict mode evidence supports combat advice.", RecommendationConfidence.Low);

        var hp = Percent(player.Hp, player.MaxHp);
        var targetHp = state.Target?.Hp is uint current && state.Target.MaxHp is uint maximum && maximum > 0
            ? Percent(current, maximum)
            : (float?)null;
        var nearbyEnemies = facts.EnemiesWithin15Yalms;
        var nearbyEnemiesInSpellRange = facts.EnemiesWithin25Yalms;
        var nearbyAllies = facts.AlliesWithin15Yalms;
        var canSpendMp = player.Mp >= CommonActionMpCost;
        var purifyReady = facts.Readiness("Purify") == ActionReadiness.Ready;
        var guardReady = facts.Readiness("Guard") == ActionReadiness.Ready;
        var forteReady = facts.Readiness("Forte") == ActionReadiness.Ready;
        var corpsReady = facts.Readiness("Corps-a-corps") == ActionReadiness.Ready;
        var displacementReady = facts.Readiness("Displacement") == ActionReadiness.Ready;
        var elixirReady = facts.Readiness("Standard-issue Elixir") == ActionReadiness.Ready;
        var riposteReady = facts.Readiness("Enchanted Riposte") == ActionReadiness.Ready;
        var emboldenReady = facts.Readiness("Embolden") == ActionReadiness.Ready;
        var resolutionReady = facts.Readiness("Resolution") == ActionReadiness.Ready;
        var bestTarget = TargetEvaluator.FindBest(state);
        var crowdControl = FindStatus(player.Statuses, PurifiableStatuses);
        var selfGuarding = facts.PlayerGuarding;
        var selfInvincible = facts.PlayerProtected;
        var targetGuarding = facts.TargetGuarding;
        var targetInvincible = facts.TargetProtected;
        var targetHasMonomachy = HasStatus(state.Target?.Statuses, "Monomachy");
        var dualcastReady = HasStatus(player.Statuses, "Dualcast");
        var prefulgenceReady = HasStatus(player.Statuses, "Prefulgence Ready");
        var thornedFlourish = HasStatus(player.Statuses, "Thorned Flourish");
        var enchantedRiposte = HasStatus(player.Statuses, "Enchanted Riposte");
        var enchantedZwerchhau = HasStatus(player.Statuses, "Enchanted Zwerchhau");
        var enchantedRedoublement = HasStatus(player.Statuses, "Enchanted Redoublement");

        if (player.Hp == 0)
            return Recommend(DecisionPriority.Wait, "Wait for respawn", "The player is incapacitated; combat recommendations are paused.");

        if (selfInvincible)
            return Recommend(DecisionPriority.Wait, "Regroup while protected", "Spawn protection is active; rejoin nearby allies before committing offensive resources.");

        if (selfGuarding)
            return Recommend(DecisionPriority.Defend, "Hold Guard", "Guard is active; avoid canceling its protection with another action.");

        if (crowdControl != null && canSpendMp && purifyReady)
            return Recommend(DecisionPriority.Purify, "Purify", $"{crowdControl} is active and at least {CommonActionMpCost:N0} MP is available.");

        if (player.Cast?.ActionId == PvpActionIds.StandardIssueElixir)
        {
            return nearbyEnemiesInSpellRange == 0
                ? Recommend(DecisionPriority.Recover, "Finish Standard-issue Elixir", "The recovery cast is already in progress and the 25-yalm threat scan remains clear.")
                : Recommend(DecisionPriority.Defend, "Cancel Elixir and move", $"{nearbyEnemiesInSpellRange} live opponent(s) entered 25 yalms during the interruptible recovery cast.");
        }

        if (elixirReady && hp > 30f && nearbyEnemiesInSpellRange == 0 &&
            (hp <= 70f || player.Mp <= 4000))
        {
            return Recommend(
                DecisionPriority.Recover,
                "Use Standard-issue Elixir",
                $"No live opponent is within 25 yalms; safely restore from {hp:F0}% HP and {player.Mp:N0} MP before re-engaging.");
        }

        if (trend?.IsRapidDamage == true && hp <= 75f)
        {
            var action = forteReady
                ? "Use Forte"
                : canSpendMp
                    ? "Use Recuperate"
                    : guardReady ? "Use Guard" : "Kite toward your team";
            return Recommend(
                DecisionPriority.Defend,
                action,
                $"HP fell {trend.HpLostPercent:F0}% within {trend.WindowSeconds:F1} seconds; interrupt the offensive sequence and stabilize.");
        }

        if (hp <= 30f && canSpendMp)
            return Recommend(DecisionPriority.Recover, "Use Recuperate", $"HP is critical at {hp:F0}% and Recuperate MP is available.");

        if (hp <= 30f)
        {
            var action = guardReady ? "Guard and disengage" : "Disengage immediately";
            return Recommend(DecisionPriority.Retreat, action, $"HP is critical at {hp:F0}% but there is not enough MP for Recuperate.");
        }

        if (hp <= 50f && nearbyEnemies >= 2)
        {
            var action = forteReady
                ? "Use Forte"
                : canSpendMp
                    ? "Use Recuperate"
                    : guardReady ? "Use Guard" : "Kite toward your team";
            return Recommend(DecisionPriority.Defend, action, $"HP is {hp:F0}% with {nearbyEnemies} opponents inside 15 yalms.");
        }

        if (hp <= 55f && player.Mp >= 4000 && !prefulgenceReady)
            return Recommend(DecisionPriority.Recover, "Use Recuperate", $"HP is {hp:F0}% and enough MP remains for two Recuperates.");

        var target = state.Target;
        if (target == null)
        {
            if (nearbyEnemies >= 2 && nearbyAllies == 0)
                return Recommend(DecisionPriority.Reposition, "Disengage toward your team", $"{nearbyEnemies} opponents are nearby and no living ally is inside 15 yalms.");

            return bestTarget == null
                ? Recommend(DecisionPriority.Observe, "Regroup and scan", "No opponent is currently inside 25 yalms.")
                : Recommend(DecisionPriority.Target, $"Target {bestTarget.Name}", DescribeTarget(bestTarget));
        }

        // Validate the target before recommending a proc. Prefulgence, Vice of
        // Thorns, and Grand Impact do not pierce Guard and should not be wasted.
        if (targetHp is null or <= 0f)
        {
            return bestTarget != null &&
                   !SameTarget(target, bestTarget)
                ? Recommend(DecisionPriority.Target, $"Switch to {bestTarget.Name}", $"{target.Name} is not a live combat target; {DescribeTarget(bestTarget)}")
                : Recommend(DecisionPriority.Target, "Find a live target", $"{target.Name} cannot currently be pressured.");
        }

        if (targetInvincible)
        {
            return bestTarget != null &&
                   !SameTarget(target, bestTarget)
                ? Recommend(DecisionPriority.Target, $"Switch to {bestTarget.Name}", $"{target.Name} has Invincibility; {DescribeTarget(bestTarget)}")
                : Recommend(DecisionPriority.Target, "Find another target", $"{target.Name} has Invincibility and cannot be pressured effectively.");
        }

        if (targetGuarding)
        {
            if (bestTarget != null && !bestTarget.IsGuarding &&
                !SameTarget(target, bestTarget) &&
                targetHp is not null && bestTarget.HpPercent + 15f < targetHp)
                return Recommend(DecisionPriority.Target, $"Switch to {bestTarget.Name}", $"{target.Name} is Guarding; {DescribeTarget(bestTarget)}");

            if (nearbyEnemies >= 2 && nearbyAllies == 0 &&
                !enchantedRiposte && !enchantedZwerchhau)
                return Recommend(DecisionPriority.Reposition, "Disengage toward your team", $"{target.Name} is Guarding while {nearbyEnemies} opponents are nearby and no ally is in support range.");

            if (target.Distance > 5f &&
                (enchantedRiposte || enchantedZwerchhau) && target.Distance <= 25f)
                return Recommend(DecisionPriority.Reposition, "Close distance to continue the melee combo", "The active melee chain ignores Guard, but the target is outside its 5-yalm range.");

            if (target.Distance > 5f && corpsReady && riposteReady &&
                hp >= 70f && nearbyEnemies <= 2)
                return Recommend(DecisionPriority.Burst, "Corps-a-corps, then Enchanted Riposte", "The melee chain ignores Guard, and Monomachy reduces this target's return damage.");

            if (target.Distance <= 5f && riposteReady)
                return Recommend(DecisionPriority.Burst, "Use Enchanted Riposte", "The melee chain ignores Guard and is currently available.");

            return Recommend(DecisionPriority.Reposition, "Do not spend ranged burst", $"{target.Name} is Guarding and the Guard-piercing melee chain is unavailable; preserve procs or switch targets.");
        }

        if (target.Distance > 25f)
            return Recommend(DecisionPriority.Reposition, "Move into spell range", $"{target.Name} is {target.Distance:F0} yalms away; preserve ready procs until the target is within 25 yalms.");

        if (prefulgenceReady)
            return Recommend(DecisionPriority.Burst, "Use Prefulgence", "Prefulgence Ready is active; use the instant damage and party healing before it expires.");

        if (thornedFlourish)
            return Recommend(DecisionPriority.Control, "Use Vice of Thorns", "Thorned Flourish is active; Vice of Thorns adds damage and a stun.");

        if (enchantedRedoublement && target.Distance <= 25f)
        {
            if (displacementReady && target.Distance <= 5f)
                return Recommend(DecisionPriority.Burst, "Use Displacement, then Scorch", "Displacement strengthens the next spell by 15%; spend that boost on Scorch.");

            return Recommend(DecisionPriority.Burst, "Use Scorch", "The recorded combo state shows Enchanted Redoublement completed; continue into Scorch.");
        }

        if (enchantedZwerchhau && target.Distance <= 5f)
            return Recommend(DecisionPriority.Burst, "Use Enchanted Redoublement", "Continue the active melee combo before its state expires.");

        if (enchantedRiposte && target.Distance <= 5f)
            return Recommend(DecisionPriority.Burst, "Use Enchanted Zwerchhau", "Continue the active melee combo before its state expires.");

        if ((enchantedRiposte || enchantedZwerchhau) && target.Distance <= 25f)
            return Recommend(DecisionPriority.Reposition, "Close distance to continue the melee combo", $"The combo is active, but {target.Name} is {target.Distance:F0} yalms away.");

        if (bestTarget != null &&
            !SameTarget(target, bestTarget) &&
            targetHp is > 55f && bestTarget.HpPercent + TargetSwitchHpAdvantage < targetHp)
        {
            return Recommend(DecisionPriority.Target, $"Switch to {bestTarget.Name}", DescribeTarget(bestTarget));
        }

        if (nearbyEnemies >= 2 && nearbyAllies == 0)
            return Recommend(DecisionPriority.Reposition, "Disengage toward your team", $"{nearbyEnemies} opponents are inside 15 yalms and no living ally is nearby; avoid spending a new burst window alone.");

        var safeBurstWindow = hp >= 70f && player.Mp >= 4000 && nearbyEnemies <= 2 && nearbyAllies >= 1;

        if (emboldenReady && safeBurstWindow && !HasStatus(player.Statuses, "Embolden"))
            return Recommend(DecisionPriority.Burst, "Use Embolden", "A teammate is nearby and defensive resources are sufficient to begin the burst window.");

        if (resolutionReady && target.Distance <= 25f)
            return Recommend(DecisionPriority.Control, "Use Resolution", "Resolution is ready; apply its line damage and Silence before committing to melee.");

        if (dualcastReady && target.Distance <= 25f)
            return Recommend(DecisionPriority.Pressure, "Use Grand Impact", "Dualcast is active; spend Grand Impact before beginning the melee commitment.");

        if (targetHp <= 30f && hp >= 55f && nearbyEnemies <= 2)
        {
            var monomachyText = targetHasMonomachy
                ? " Monomachy is already active."
                : corpsReady ? " Corps-a-corps is ready to apply Monomachy." : " Corps-a-corps is unavailable; finish from range.";
            return Recommend(DecisionPriority.FinishTarget, "Commit to the finish", $"{target.Name} is at {targetHp:F0}% HP and the local risk is acceptable.{monomachyText}");
        }

        if (targetHasMonomachy && riposteReady && target.Distance <= 5f && hp >= 55f && nearbyEnemies <= 2)
            return Recommend(DecisionPriority.Burst, "Start Enchanted Riposte", "Monomachy is active, the melee chain is ready, and local risk is acceptable.");

        if (!targetHasMonomachy && corpsReady && riposteReady && target.Distance <= 25f &&
            hp >= 70f && player.Mp >= 4000 && nearbyEnemies <= 2 && nearbyAllies >= 1)
        {
            return Recommend(
                DecisionPriority.Burst,
                "Corps-a-corps, then Enchanted Riposte",
                $"Burst resources are ready with {hp:F0}% HP, {player.Mp:N0} MP, and {nearbyAllies} nearby ally/allies.");
        }

        if (target.Distance <= 5f && hp >= 60f && nearbyEnemies <= 2)
            return Recommend(DecisionPriority.Burst, "Continue the melee combo", $"The target is in melee range with {nearbyEnemies} nearby opponent(s) and your HP is stable.");

        if (nearbyEnemies >= 3)
            return Recommend(DecisionPriority.Reposition, "Kite toward your team", $"{nearbyEnemies} opponents are inside 15 yalms; avoid an isolated melee commitment.");

        return Recommend(DecisionPriority.Pressure, "Use Jolt III and assess", "Build Dualcast at range while preserving defensive options.");
    }

    private static float Percent(uint current, uint maximum) => current * 100f / maximum;

    private static DecisionRecommendation Recommend(DecisionPriority priority, string action, string reason,
        RecommendationConfidence confidence = RecommendationConfidence.High) =>
        new(priority, action, reason) { Confidence = confidence };

    private static string? FindStatus(IEnumerable<StatusSnapshot>? statuses, HashSet<string> names)
    {
        if (statuses == null)
            return null;

        foreach (var status in statuses)
        {
            if (names.Contains(status.Name))
                return status.Name;
        }

        return null;
    }

    private static bool HasStatus(IEnumerable<StatusSnapshot>? statuses, string name)
    {
        if (statuses == null)
            return false;

        foreach (var status in statuses)
        {
            if (string.Equals(status.Name, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsActionAvailable(IEnumerable<ActionCooldownSnapshot> actions, string name)
    {
        var found = false;
        foreach (var action in actions)
        {
            if (!string.Equals(action.Name, name, StringComparison.OrdinalIgnoreCase))
                continue;

            found = true;
            if (action.IsAvailable)
                return true;
        }

        // Offline scenarios and old recordings have no cooldown list. Preserve
        // their prior behavior instead of treating every action as unavailable.
        return !found;
    }

    private static bool IsActionKnownAndAvailable(IEnumerable<ActionCooldownSnapshot> actions, string name)
    {
        foreach (var action in actions)
        {
            if (string.Equals(action.Name, name, StringComparison.OrdinalIgnoreCase))
                return action.IsAvailable;
        }

        return false;
    }

    private static string DescribeTarget(TargetCandidate target)
    {
        var guardText = target.IsGuarding ? ", Guarding" : "";
        var markText = target.HasMonomachy ? ", Monomachy active" : "";
        return $"{target.Job} at {target.HpPercent:F0}% HP and {target.Distance:F0} yalms{guardText}{markText}.";
    }

    private static bool SameTarget(TargetSnapshot target, TargetCandidate candidate) =>
        target.ObjectId != 0 && candidate.ObjectId != 0
            ? target.ObjectId == candidate.ObjectId
            : string.Equals(target.Name, candidate.Name, StringComparison.Ordinal);
}

public enum DecisionPriority
{
    Wait,
    Observe,
    Target,
    Pressure,
    Reposition,
    Control,
    Burst,
    FinishTarget,
    Defend,
    Recover,
    Purify,
    Retreat
}

public enum RecommendationConfidence { Low, Medium, High }

public sealed record DecisionRecommendation(DecisionPriority Priority, string Recommendation, string Reason)
{
    public RecommendationConfidence Confidence { get; init; } = RecommendationConfidence.High;
}
