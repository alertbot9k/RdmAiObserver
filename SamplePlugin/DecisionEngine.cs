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

    private static readonly HashSet<string> PurifiableStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Stun", "Heavy", "Bind", "Silence", "Deep Freeze", "Miracle of Nature"
    };

    public static DecisionRecommendation Evaluate(GameState state)
    {
        var player = state.Player;
        if (player == null || player.MaxHp == 0)
            return Recommend(DecisionPriority.Wait, "Wait for player data", "The player snapshot is incomplete.");

        var hp = Percent(player.Hp, player.MaxHp);
        var targetHp = state.Target?.Hp is uint current && state.Target.MaxHp is uint maximum && maximum > 0
            ? Percent(current, maximum)
            : (float?)null;
        var nearbyEnemies = CountNearbyEnemies(state, 15f);
        var canSpendMp = player.Mp >= CommonActionMpCost;
        var purifyReady = IsActionAvailable(player.Actions, "Purify");
        var guardReady = IsActionAvailable(player.Actions, "Guard");
        var forteReady = IsActionAvailable(player.Actions, "Forte");
        var corpsReady = IsActionAvailable(player.Actions, "Corps-a-corps");
        var riposteReady = IsActionAvailable(player.Actions, "Enchanted Riposte");
        var emboldenReady = IsActionAvailable(player.Actions, "Embolden");
        var bestTarget = TargetEvaluator.FindBest(state);
        var crowdControl = FindStatus(player.Statuses, PurifiableStatuses);
        var selfGuarding = HasStatus(player.Statuses, "Guard");
        var targetGuarding = HasStatus(state.Target?.Statuses, "Guard");
        var targetHasMonomachy = HasStatus(state.Target?.Statuses, "Monomachy");
        var dualcastReady = HasStatus(player.Statuses, "Dualcast");
        var prefulgenceReady = HasStatus(player.Statuses, "Prefulgence Ready");
        var thornedFlourish = HasStatus(player.Statuses, "Thorned Flourish");
        var enchantedRiposte = HasStatus(player.Statuses, "Enchanted Riposte");
        var enchantedZwerchhau = HasStatus(player.Statuses, "Enchanted Zwerchhau");
        var enchantedRedoublement = HasStatus(player.Statuses, "Enchanted Redoublement");

        if (player.Hp == 0)
            return Recommend(DecisionPriority.Wait, "Wait for respawn", "The player is incapacitated; combat recommendations are paused.");

        if (selfGuarding)
            return Recommend(DecisionPriority.Defend, "Hold Guard", "Guard is active; avoid canceling its protection with another action.");

        if (crowdControl != null && canSpendMp && purifyReady)
            return Recommend(DecisionPriority.Purify, "Purify", $"{crowdControl} is active and at least {CommonActionMpCost:N0} MP is available.");

        if (hp <= 30f && canSpendMp)
            return Recommend(DecisionPriority.Recover, "Use Recuperate", $"HP is critical at {hp:F0}% and Recuperate MP is available.");

        if (hp <= 30f)
        {
            var action = guardReady ? "Guard and disengage" : "Disengage immediately";
            return Recommend(DecisionPriority.Retreat, action, $"HP is critical at {hp:F0}% but there is not enough MP for Recuperate.");
        }

        if (hp <= 50f && nearbyEnemies >= 2)
        {
            var action = forteReady ? "Use Forte" : guardReady ? "Use Guard" : "Kite toward your team";
            return Recommend(DecisionPriority.Defend, action, $"HP is {hp:F0}% with {nearbyEnemies} opponents inside 15 yalms.");
        }

        if (prefulgenceReady && state.Target != null)
            return Recommend(DecisionPriority.Burst, "Use Prefulgence", "Prefulgence Ready is active; use the instant damage and party healing before it expires.");

        if (thornedFlourish && state.Target != null)
            return Recommend(DecisionPriority.Control, "Use Vice of Thorns", "Thorned Flourish is active; Vice of Thorns adds damage and a stun.");

        if (state.Target == null)
        {
            return bestTarget == null
                ? Recommend(DecisionPriority.Observe, "Select a vulnerable target", "No target is selected and no opponent is inside 25 yalms.")
                : Recommend(DecisionPriority.Target, $"Target {bestTarget.Name}", DescribeTarget(bestTarget));
        }

        if (bestTarget != null &&
            !string.Equals(state.Target.Name, bestTarget.Name, StringComparison.Ordinal) &&
            targetHp is > 55f && bestTarget.HpPercent + 15f < targetHp)
        {
            return Recommend(DecisionPriority.Target, $"Switch to {bestTarget.Name}", DescribeTarget(bestTarget));
        }

        if (targetGuarding && state.Target.Distance > 5f)
            return Recommend(DecisionPriority.Reposition, "Do not spend ranged burst", $"{state.Target.Name} is Guarding; reposition or pressure a different target.");

        if (targetGuarding && state.Target.Distance <= 5f && riposteReady)
            return Recommend(DecisionPriority.Burst, "Use Enchanted Riposte", "The melee chain ignores Guard and is currently available.");

        if (enchantedRedoublement && state.Target.Distance <= 25f)
            return Recommend(DecisionPriority.Burst, "Use Scorch", "The recorded combo state shows Enchanted Redoublement completed; continue into Scorch.");

        if (enchantedZwerchhau && state.Target.Distance <= 5f)
            return Recommend(DecisionPriority.Burst, "Use Enchanted Redoublement", "Continue the active melee combo before its state expires.");

        if (enchantedRiposte && state.Target.Distance <= 5f)
            return Recommend(DecisionPriority.Burst, "Use Enchanted Zwerchhau", "Continue the active melee combo before its state expires.");

        if (targetHp <= 30f && hp >= 55f && nearbyEnemies <= 2)
        {
            var monomachyText = targetHasMonomachy
                ? " Monomachy is already active."
                : corpsReady ? " Corps-a-corps is ready to apply Monomachy." : " Corps-a-corps is unavailable; finish from range.";
            return Recommend(DecisionPriority.FinishTarget, "Commit to the finish", $"{state.Target.Name} is at {targetHp:F0}% HP and the local risk is acceptable.{monomachyText}");
        }

        if (targetHasMonomachy && riposteReady && state.Target.Distance <= 5f && hp >= 55f && nearbyEnemies <= 2)
            return Recommend(DecisionPriority.Burst, "Start Enchanted Riposte", "Monomachy is active, the melee chain is ready, and local risk is acceptable.");

        if (!targetHasMonomachy && corpsReady && riposteReady && state.Target.Distance <= 25f && hp >= 65f && nearbyEnemies <= 2)
            return Recommend(DecisionPriority.Burst, "Corps-a-corps, then Enchanted Riposte", "Both engagement and melee-chain resources are ready for a controlled burst window.");

        if (emboldenReady && targetHp <= 70f && hp >= 55f && nearbyEnemies <= 2 && !HasStatus(player.Statuses, "Embolden"))
            return Recommend(DecisionPriority.Burst, "Use Embolden", "A viable target is present and the defensive risk is acceptable for a team burst window.");

        if (dualcastReady && state.Target.Distance <= 25f)
            return Recommend(DecisionPriority.Pressure, "Use Grand Impact", "Dualcast is active and the target is in spell range.");

        if (state.Target.Distance <= 5f && hp >= 60f && nearbyEnemies <= 2)
            return Recommend(DecisionPriority.Burst, "Continue the melee combo", $"The target is in melee range with {nearbyEnemies} nearby opponent(s) and your HP is stable.");

        if (state.Target.Distance > 25f)
            return Recommend(DecisionPriority.Reposition, "Move into spell range", $"{state.Target.Name} is {state.Target.Distance:F0} yalms away.");

        if (nearbyEnemies >= 3)
            return Recommend(DecisionPriority.Reposition, "Kite toward your team", $"{nearbyEnemies} opponents are inside 15 yalms; avoid an isolated melee commitment.");

        return Recommend(DecisionPriority.Pressure, "Use Jolt III and assess", "Build Dualcast at range while preserving defensive options.");
    }

    private static float Percent(uint current, uint maximum) => current * 100f / maximum;

    private static DecisionRecommendation Recommend(DecisionPriority priority, string action, string reason) =>
        new(priority, action, reason);

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

    private static int CountNearbyEnemies(GameState state, float range)
    {
        var partyNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in state.Party)
            partyNames.Add(member.Name);

        var count = 0;
        foreach (var character in state.NearbyCharacters)
        {
            if (character.Distance <= range && !partyNames.Contains(character.Name))
                count++;
        }

        return count;
    }

    private static string DescribeTarget(TargetCandidate target)
    {
        var guardText = target.IsGuarding ? ", Guarding" : "";
        var markText = target.HasMonomachy ? ", Monomachy active" : "";
        return $"{target.Job} at {target.HpPercent:F0}% HP and {target.Distance:F0} yalms{guardText}{markText}.";
    }
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

public sealed record DecisionRecommendation(DecisionPriority Priority, string Recommendation, string Reason);
